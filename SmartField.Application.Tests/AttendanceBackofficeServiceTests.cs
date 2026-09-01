using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class AttendanceBackofficeServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("05c6ecda-1c03-4a45-972b-45f3a63d1dd8");
    private static readonly Guid OtherCompanyId = Guid.Parse("8fdf3240-921f-41d2-983d-6761bc1d5756");
    private static readonly Guid UserId = Guid.Parse("c8fdb9d7-911b-4b27-a85e-7da9c2082d0e");
    private static readonly Guid EmployeeId = Guid.Parse("d58d46e2-cb7a-4d1a-b510-365fe8e9f12a");
    private static readonly Guid SecondEmployeeId = Guid.Parse("dd6ff776-6f7a-4dbf-b83f-50bd492911f1");
    private static readonly Guid WorkSiteId = Guid.Parse("58e0bdd2-c572-4310-9f1f-7c06914b4505");
    private static readonly Guid SecondWorkSiteId = Guid.Parse("2602d54c-6c8d-44dd-8c9a-0a720674520e");
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetBackofficeDayAsync_ListsEmployeesAndCalculatesDailyState()
    {
        var store = CreateStore();
        store.Events.AddRange(
        [
            CreateEvent(EmployeeId, AttendanceEventType.ClockIn, 8, 57, workSiteId: WorkSiteId),
            CreateEvent(EmployeeId, AttendanceEventType.BreakStart, 12, 0, workSiteId: WorkSiteId),
            CreateEvent(EmployeeId, AttendanceEventType.BreakEnd, 12, 30, workSiteId: WorkSiteId),
            CreateEvent(SecondEmployeeId, AttendanceEventType.ClockIn, 9, 2, workSiteId: SecondWorkSiteId),
            CreateEvent(SecondEmployeeId, AttendanceEventType.ClockOut, 17, 58, workSiteId: SecondWorkSiteId, isInsideGeofence: false)
        ]);
        var service = CreateService(store);

        var result = await service.GetBackofficeDayAsync(
            new AttendanceBackofficeDayFilter(new DateOnly(2026, 9, 1), null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-09-01", result.Value?.Date);
        Assert.Equal(2, result.Value?.Employees.Count);

        var joao = result.Value!.Employees.Single(row => row.EmployeeId == EmployeeId);
        Assert.Equal("Joao Silva", joao.EmployeeName);
        Assert.Equal("Working", joao.CurrentStatus);
        Assert.Equal("EM TRABALHO", joao.CurrentStatusLabel);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 57, 0, TimeSpan.Zero), joao.ClockIn);
        Assert.Null(joao.ClockOut);
        Assert.Equal(333, joao.WorkedMinutes);
        Assert.Equal(30, joao.BreakMinutes);
        Assert.Equal(1, joao.BreakCount);

        var maria = result.Value.Employees.Single(row => row.EmployeeId == SecondEmployeeId);
        Assert.Equal("Closed", maria.CurrentStatus);
        Assert.True(maria.HasOutsideGeofence);
    }

    [Fact]
    public async Task GetBackofficeDayAsync_FiltersByEmployeeAndWorkSiteInsideCompany()
    {
        var store = CreateStore();
        store.Events.AddRange(
        [
            CreateEvent(EmployeeId, AttendanceEventType.ClockIn, 8, 0, workSiteId: WorkSiteId),
            CreateEvent(SecondEmployeeId, AttendanceEventType.ClockIn, 9, 0, workSiteId: SecondWorkSiteId),
            CreateEvent(EmployeeId, AttendanceEventType.ClockIn, 7, 0, companyId: OtherCompanyId, workSiteId: WorkSiteId)
        ]);
        var service = CreateService(store);

        var result = await service.GetBackofficeDayAsync(
            new AttendanceBackofficeDayFilter(
                new DateOnly(2026, 9, 1),
                null,
                WorkSiteId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value!.Employees);
        Assert.Equal(EmployeeId, row.EmployeeId);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), row.ClockIn);
    }

    [Fact]
    public async Task GetBackofficeDayDetailAsync_ReturnsOriginalEventsBreaksAndWarning()
    {
        var store = CreateStore();
        store.Events.AddRange(
        [
            CreateEvent(EmployeeId, AttendanceEventType.ClockIn, 8, 0, workSiteId: WorkSiteId),
            CreateEvent(EmployeeId, AttendanceEventType.BreakStart, 12, 0, workSiteId: WorkSiteId),
            CreateEvent(EmployeeId, AttendanceEventType.BreakEnd, 12, 15, workSiteId: WorkSiteId, isInsideGeofence: false),
            CreateEvent(EmployeeId, AttendanceEventType.ClockOut, 17, 0, workSiteId: WorkSiteId)
        ]);
        var service = CreateService(store);

        var result = await service.GetBackofficeDayDetailAsync(
            EmployeeId,
            new DateOnly(2026, 9, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeId, result.Value?.EmployeeId);
        Assert.Equal("Joao Silva", result.Value?.EmployeeName);
        Assert.Equal(525, result.Value?.WorkedMinutes);
        Assert.Equal(15, result.Value?.BreakMinutes);
        Assert.True(result.Value?.HasOutsideGeofence);
        Assert.Single(result.Value!.Breaks);
        Assert.Equal(4, result.Value.Events.Count);
        Assert.Equal("ClockIn", result.Value.Events[0].EventType);
        Assert.Equal("ClockOut", result.Value.Events[^1].EventType);
    }

    [Fact]
    public async Task GetBackofficeDayDetailAsync_ReturnsNotFoundForForeignEmployee()
    {
        var service = CreateService(CreateStore());

        var result = await service.GetBackofficeDayDetailAsync(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            CancellationToken.None);

        Assert.Equal(AttendanceError.EmployeeNotFound, result.Error);
    }

    private static AttendanceService CreateService(FakeAttendanceStore store)
    {
        return new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(),
            new FakeCurrentUserProvider(),
            new FakeGeolocationService(),
            new FixedTimeProvider());
    }

    private static FakeAttendanceStore CreateStore()
    {
        var store = new FakeAttendanceStore();
        store.Employees.AddRange(
        [
            new AttendanceBackofficeEmployeeReference(
                EmployeeId,
                "FUNC001",
                "Joao Silva",
                WorkSiteId,
                "Sede"),
            new AttendanceBackofficeEmployeeReference(
                SecondEmployeeId,
                "FUNC002",
                "Maria Costa",
                SecondWorkSiteId,
                "Armazem")
        ]);

        return store;
    }

    private static AttendanceEvent CreateEvent(
        Guid employeeId,
        AttendanceEventType eventType,
        int hour,
        int minute,
        Guid? companyId = null,
        Guid? workSiteId = null,
        bool? isInsideGeofence = true)
    {
        var timestamp = new DateTimeOffset(2026, 9, 1, hour, minute, 0, TimeSpan.Zero);
        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? CompanyId,
            EmployeeId = employeeId,
            EventType = eventType,
            ServerTimestampUtc = timestamp,
            CreatedAtUtc = timestamp,
            ClientEventId = Guid.NewGuid(),
            WorkSiteId = workSiteId,
            IsInsideGeofence = isInsideGeofence
        };
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public Guid? CompanyId => AttendanceBackofficeServiceTests.CompanyId;
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? UserId => AttendanceBackofficeServiceTests.UserId;
        public Guid? EmployeeId => AttendanceBackofficeServiceTests.EmployeeId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAttendanceStore : IAttendanceStore
    {
        public List<AttendanceBackofficeEmployeeReference> Employees { get; } = [];
        public List<AttendanceEvent> Events { get; } = [];

        public Task<bool> EmployeeCanPunchAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId && employeeId == EmployeeId);
        }

        public Task<bool> ProjectExistsAsync(
            Guid companyId,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<AttendanceEvent?> GetByClientEventIdAsync(
            Guid companyId,
            Guid employeeId,
            Guid clientEventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AttendanceEvent?>(null);
        }

        public Task<AttendanceEventType?> GetLastEventTypeAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AttendanceEventType?>(null);
        }

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AttendanceEmployeeStateReference?>(null);
        }

        public Task<string?> GetCompanyTimeZoneAsync(
            Guid companyId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(companyId == CompanyId ? "UTC" : null);
        }

        public Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
            Guid companyId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceBackofficeEmployeeReference> employees =
                companyId == CompanyId
                    ? Employees
                        .Where(employee =>
                            !employeeId.HasValue || employee.EmployeeId == employeeId.Value)
                        .ToArray()
                    : [];

            return Task.FromResult(employees);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
            Guid companyId,
            Guid employeeId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceEvent> events = Events
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.EmployeeId == employeeId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc)
                .ToArray();

            return Task.FromResult(events);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
            Guid companyId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceEvent> events = Events
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc
                    && attendanceEvent.ServerTimestampUtc < toUtc
                    && (!employeeId.HasValue || attendanceEvent.EmployeeId == employeeId.Value))
                .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
                .ToArray();

            return Task.FromResult(events);
        }

        public void Add(AttendanceEvent attendanceEvent) => throw new NotSupportedException();
        public void Add(AuditLog auditLog) => throw new NotSupportedException();
        public void Add(IntegrationOutbox integrationOutbox) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
