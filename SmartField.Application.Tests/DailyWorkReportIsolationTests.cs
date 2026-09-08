using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using DomainIntegrationOutbox = SmartField.Domain.Entities.IntegrationOutbox;

namespace SmartField.Application.Tests;

public class DailyWorkReportIsolationTests
{
    private static readonly Guid CompanyAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EmployeeAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid EmployeeBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly WorkDate = new(2026, 9, 8);
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetDayAsync_DoesNotReturnAnotherEmployeesDailyReport()
    {
        var store = new FakeAttendanceStore();
        store.DailyWorkReports.Add(new DailyWorkReport
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyAId,
            EmployeeId = EmployeeBId,
            WorkDate = WorkDate,
            Summary = "Resumo privado do outro funcionário.",
            ClockOutAttendanceEventId = Guid.NewGuid(),
            SubmittedAtUtc = Now,
            CreatedAtUtc = Now
        });

        var service = CreateService(store, CompanyAId, EmployeeAId);

        var result = await service.GetDayAsync(WorkDate, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.DailySummary);
        Assert.Equal((CompanyAId, EmployeeAId, WorkDate), store.LastDailyReportQuery);
    }

    [Fact]
    public async Task GetBackofficeDayDetailAsync_DoesNotExposeEmployeeFromAnotherCompany()
    {
        var store = new FakeAttendanceStore();
        store.Employees.Add(new ScopedEmployee(
            CompanyAId,
            new AttendanceBackofficeEmployeeReference(
                EmployeeAId,
                "FUNC001",
                "João Silva",
                null,
                null)));
        store.DailyWorkReports.Add(new DailyWorkReport
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyAId,
            EmployeeId = EmployeeAId,
            WorkDate = WorkDate,
            Summary = "Resumo da Empresa A.",
            ClockOutAttendanceEventId = Guid.NewGuid(),
            SubmittedAtUtc = Now,
            CreatedAtUtc = Now
        });

        var service = CreateService(store, CompanyBId, null);

        var result = await service.GetBackofficeDayDetailAsync(
            EmployeeAId,
            WorkDate,
            CancellationToken.None);

        Assert.Equal(AttendanceError.EmployeeNotFound, result.Error);
        Assert.Equal(CompanyBId, store.LastBackofficeCompanyId);
        Assert.Equal(0, store.DailyReportQueryCount);
    }

    private static AttendanceService CreateService(
        FakeAttendanceStore store,
        Guid companyId,
        Guid? employeeId)
    {
        return new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(companyId),
            new FakeCurrentUserProvider(UserId, employeeId),
            new FakeGeolocationService(),
            new IntegrationOutboxService(store),
            new FixedTimeProvider());
    }

    private sealed record ScopedEmployee(
        Guid CompanyId,
        AttendanceBackofficeEmployeeReference Employee);

    private sealed class FakeCurrentCompanyProvider(Guid companyId) : ICurrentCompanyProvider
    {
        public Guid? CompanyId => companyId;
    }

    private sealed class FakeCurrentUserProvider(Guid userId, Guid? employeeId) : ICurrentUserProvider
    {
        public Guid? UserId => userId;
        public Guid? EmployeeId => employeeId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAttendanceStore : IAttendanceStore, IIntegrationOutboxStore
    {
        public List<DailyWorkReport> DailyWorkReports { get; } = [];
        public List<ScopedEmployee> Employees { get; } = [];
        public (Guid CompanyId, Guid EmployeeId, DateOnly WorkDate)? LastDailyReportQuery { get; private set; }
        public Guid? LastBackofficeCompanyId { get; private set; }
        public int DailyReportQueryCount { get; private set; }

        public Task<DailyWorkReport?> GetDailyWorkReportAsync(
            Guid companyId,
            Guid employeeId,
            DateOnly workDate,
            CancellationToken cancellationToken)
        {
            DailyReportQueryCount++;
            LastDailyReportQuery = (companyId, employeeId, workDate);
            return Task.FromResult(DailyWorkReports.SingleOrDefault(report =>
                report.CompanyId == companyId
                && report.EmployeeId == employeeId
                && report.WorkDate == workDate));
        }

        public void Add(DailyWorkReport report) => DailyWorkReports.Add(report);

        public Task<bool> EmployeeCanPunchAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> ProjectExistsAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<AttendanceEvent?> GetByClientEventIdAsync(
            Guid companyId,
            Guid employeeId,
            Guid clientEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AttendanceEvent?>(null);

        public Task<AttendanceEvent?> GetEventAsync(
            Guid companyId,
            Guid attendanceEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AttendanceEvent?>(null);

        public Task<AttendanceEventType?> GetLastEventTypeAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AttendanceEventType?>(null);

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            if (companyId != CompanyAId || employeeId != EmployeeAId)
            {
                return Task.FromResult<AttendanceEmployeeStateReference?>(null);
            }

            return Task.FromResult<AttendanceEmployeeStateReference?>(
                new AttendanceEmployeeStateReference(
                    EmployeeAId,
                    "João Silva",
                    "UTC",
                    null));
        }

        public Task<string?> GetCompanyTimeZoneAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(companyId is var id && (id == CompanyAId || id == CompanyBId) ? "UTC" : null);

        public Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
            Guid companyId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            LastBackofficeCompanyId = companyId;
            IReadOnlyList<AttendanceBackofficeEmployeeReference> result = Employees
                .Where(item => item.CompanyId == companyId)
                .Select(item => item.Employee)
                .Where(item => !employeeId.HasValue || item.EmployeeId == employeeId.Value)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
            Guid companyId,
            Guid employeeId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceEvent>>([]);

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
            Guid companyId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? employeeId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceEvent>>([]);

        public Task<IReadOnlyList<AttendanceEventCorrectionReference>> GetCorrectionsForEventsAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> attendanceEventIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceEventCorrectionReference>>([]);

        public Task<IReadOnlyList<AttendanceReferenceLookup>> GetWorkSiteReferencesAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> workSiteIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceReferenceLookup>>([]);

        public Task<IReadOnlyList<AttendanceReferenceLookup>> GetProjectReferencesAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AttendanceReferenceLookup>>([]);

        public void Add(AttendanceEvent attendanceEvent) { }
        public void Add(AttendanceCorrection attendanceCorrection) { }
        public void Add(AuditLog auditLog) { }
        public void Add(DomainIntegrationOutbox integrationOutbox) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
