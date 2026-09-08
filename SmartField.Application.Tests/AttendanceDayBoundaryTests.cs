using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using DomainIntegrationOutbox = SmartField.Domain.Entities.IntegrationOutbox;

namespace SmartField.Application.Tests;

public class AttendanceDayBoundaryTests
{
    private static readonly Guid CompanyId = Guid.Parse("3c7a2a1e-9c7c-40ef-8f72-2266d25d4746");
    private static readonly Guid UserId = Guid.Parse("8bb235d8-f7b1-45b5-a9d2-498ee8704e7f");
    private static readonly Guid EmployeeId = Guid.Parse("af441541-7a8d-47aa-aeec-d343074a1742");

    [Theory]
    [InlineData("2026-02-15", "2026-02-15T00:00:00+00:00", "2026-02-16T00:00:00+00:00", 24, "2026-02-15T12:00:00+00:00")]
    [InlineData("2026-03-29", "2026-03-29T00:00:00+00:00", "2026-03-29T23:00:00+00:00", 23, "2026-03-29T11:00:00+00:00")]
    [InlineData("2026-10-25", "2026-10-24T23:00:00+00:00", "2026-10-26T00:00:00+00:00", 25, "2026-10-25T12:00:00+00:00")]
    public async Task GetTodayAsync_UsesCivilDayBoundariesInEuropeLisbon(
        string localDate,
        string expectedStartUtc,
        string expectedEndUtc,
        int expectedHours,
        string serverNowUtc)
    {
        var startUtc = DateTimeOffset.Parse(expectedStartUtc);
        var endUtc = DateTimeOffset.Parse(expectedEndUtc);
        var store = CreateStore(startUtc, endUtc);
        var service = CreateService(store, DateTimeOffset.Parse(serverNowUtc));

        var result = await service.GetTodayAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DateTimeOffset.Parse(expectedStartUtc), store.LastFromUtc);
        Assert.Equal(expectedHours, (endUtc - startUtc).TotalHours);
        Assert.Equal(2, result.Value!.Events.Count);
        Assert.Equal(startUtc, result.Value.Events[0].ServerTimestampUtc);
        Assert.Equal(endUtc.AddMinutes(-1), result.Value.Events[1].ServerTimestampUtc);
        Assert.Equal("Closed", result.Value.CurrentStatus);
        Assert.Equal(localDate, DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(DateTimeOffset.Parse(serverNowUtc), GetLisbonTimeZone()).Date)
            .ToString("yyyy-MM-dd"));
    }

    [Theory]
    [InlineData("2026-02-15T00:00:00+00:00", "2026-02-16T00:00:00+00:00", "2026-02-15T12:00:00+00:00")]
    [InlineData("2026-03-29T00:00:00+00:00", "2026-03-29T23:00:00+00:00", "2026-03-29T11:00:00+00:00")]
    [InlineData("2026-10-24T23:00:00+00:00", "2026-10-26T00:00:00+00:00", "2026-10-25T12:00:00+00:00")]
    public async Task GetStateAsync_UsesSameDstSafeBoundariesAsDayQueries(
        string expectedStartUtc,
        string expectedEndUtc,
        string serverNowUtc)
    {
        var startUtc = DateTimeOffset.Parse(expectedStartUtc);
        var endUtc = DateTimeOffset.Parse(expectedEndUtc);
        var store = CreateStore(startUtc, endUtc);
        var service = CreateService(store, DateTimeOffset.Parse(serverNowUtc));

        var result = await service.GetStateAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(startUtc, store.LastFromUtc);
        Assert.Equal(startUtc, result.Value!.ClockInAtUtc);
        Assert.Equal("ClockOut", result.Value.LastEventType);
    }

    private static FakeAttendanceStore CreateStore(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var store = new FakeAttendanceStore
        {
            LastEventType = AttendanceEventType.ClockOut
        };
        store.Events.AddRange(
        [
            CreateEvent(AttendanceEventType.ClockIn, startUtc.AddMinutes(-1)),
            CreateEvent(AttendanceEventType.ClockIn, startUtc),
            CreateEvent(AttendanceEventType.ClockOut, endUtc.AddMinutes(-1)),
            CreateEvent(AttendanceEventType.ClockIn, endUtc)
        ]);
        return store;
    }

    private static AttendanceService CreateService(FakeAttendanceStore store, DateTimeOffset nowUtc)
    {
        return new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(),
            new FakeCurrentUserProvider(),
            new FakeGeolocationService(),
            new IntegrationOutboxService(store),
            new FixedTimeProvider(nowUtc));
    }

    private static AttendanceEvent CreateEvent(
        AttendanceEventType eventType,
        DateTimeOffset timestampUtc)
    {
        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            EmployeeId = EmployeeId,
            EventType = eventType,
            ServerTimestampUtc = timestampUtc,
            CreatedAtUtc = timestampUtc,
            ClientEventId = Guid.NewGuid()
        };
    }

    private static TimeZoneInfo GetLisbonTimeZone() =>
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public Guid? CompanyId => AttendanceDayBoundaryTests.CompanyId;
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? UserId => AttendanceDayBoundaryTests.UserId;
        public Guid? EmployeeId => AttendanceDayBoundaryTests.EmployeeId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAttendanceStore : IAttendanceStore, IIntegrationOutboxStore
    {
        public List<AttendanceEvent> Events { get; } = [];
        public DateTimeOffset? LastFromUtc { get; private set; }
        public AttendanceEventType? LastEventType { get; init; }

        public Task<bool> EmployeeCanPunchAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken) =>
            Task.FromResult(companyId == CompanyId && employeeId == EmployeeId);

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AttendanceEmployeeStateReference?>(
                new AttendanceEmployeeStateReference(EmployeeId, "Employee DST", "Europe/Lisbon", null));

        public Task<AttendanceEventType?> GetLastEventTypeAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken) => Task.FromResult(LastEventType);

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
            Guid companyId,
            Guid employeeId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken)
        {
            LastFromUtc = fromUtc;
            IReadOnlyList<AttendanceEvent> result = Events
                .Where(item => item.CompanyId == companyId
                    && item.EmployeeId == employeeId
                    && item.ServerTimestampUtc >= fromUtc)
                .OrderBy(item => item.ServerTimestampUtc)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<string?> GetCompanyTimeZoneAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(companyId == CompanyId ? "Europe/Lisbon" : null);

        public Task<DailyWorkReport?> GetDailyWorkReportAsync(Guid companyId, Guid employeeId, DateOnly workDate, CancellationToken cancellationToken) =>
            Task.FromResult<DailyWorkReport?>(null);

        public Task<bool> ProjectExistsAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<AttendanceEvent?> GetByClientEventIdAsync(Guid companyId, Guid employeeId, Guid clientEventId, CancellationToken cancellationToken) => Task.FromResult<AttendanceEvent?>(null);
        public Task<AttendanceEvent?> GetEventAsync(Guid companyId, Guid attendanceEventId, CancellationToken cancellationToken) => Task.FromResult<AttendanceEvent?>(null);
        public Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(Guid companyId, Guid? employeeId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttendanceBackofficeEmployeeReference>>([]);
        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(Guid companyId, DateTimeOffset fromUtc, DateTimeOffset toUtc, Guid? employeeId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttendanceEvent>>([]);
        public Task<IReadOnlyList<AttendanceEventCorrectionReference>> GetCorrectionsForEventsAsync(Guid companyId, IReadOnlyCollection<Guid> attendanceEventIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttendanceEventCorrectionReference>>([]);
        public Task<IReadOnlyList<AttendanceReferenceLookup>> GetWorkSiteReferencesAsync(Guid companyId, IReadOnlyCollection<Guid> workSiteIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttendanceReferenceLookup>>([]);
        public Task<IReadOnlyList<AttendanceReferenceLookup>> GetProjectReferencesAsync(Guid companyId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AttendanceReferenceLookup>>([]);

        public void Add(DailyWorkReport report) => throw new NotSupportedException();
        public void Add(AttendanceEvent attendanceEvent) => throw new NotSupportedException();
        public void Add(AttendanceCorrection attendanceCorrection) => throw new NotSupportedException();
        public void Add(AuditLog auditLog) => throw new NotSupportedException();
        public void Add(DomainIntegrationOutbox integrationOutbox) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
