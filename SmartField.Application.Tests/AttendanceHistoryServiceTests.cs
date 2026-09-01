using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class AttendanceHistoryServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetHistoryAsync_GroupsDaysNewestFirstAndCalculatesTotals()
    {
        var store = new FakeAttendanceStore();
        store.Events.AddRange(
        [
            CreateEvent(AttendanceEventType.ClockIn, 2026, 8, 31, 8, 0),
            CreateEvent(AttendanceEventType.BreakStart, 2026, 8, 31, 12, 0),
            CreateEvent(AttendanceEventType.BreakEnd, 2026, 8, 31, 12, 30),
            CreateEvent(AttendanceEventType.ClockOut, 2026, 8, 31, 17, 0),
            CreateEvent(AttendanceEventType.ClockIn, 2026, 9, 1, 9, 0, isInsideGeofence: false),
            CreateEvent(AttendanceEventType.ClockOut, 2026, 9, 1, 13, 0)
        ]);
        var service = CreateService(store);

        var result = await service.GetHistoryAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value?.Count);

        var latest = result.Value![0];
        Assert.Equal("2026-09-01", latest.Date);
        Assert.Equal(240, latest.WorkedMinutes);
        Assert.Equal(0, latest.BreakMinutes);
        Assert.True(latest.HasOutsideGeofence);

        var previous = result.Value[1];
        Assert.Equal("2026-08-31", previous.Date);
        Assert.Equal(510, previous.WorkedMinutes);
        Assert.Equal(30, previous.BreakMinutes);
        Assert.Equal(1, previous.BreakCount);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyListWhenEmployeeHasNoEvents()
    {
        var service = CreateService(new FakeAttendanceStore());

        var result = await service.GetHistoryAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetDayAsync_ReturnsBreaksEventsAndGeofenceWarning()
    {
        var store = new FakeAttendanceStore();
        store.Events.AddRange(
        [
            CreateEvent(AttendanceEventType.ClockIn, 2026, 8, 31, 8, 0),
            CreateEvent(AttendanceEventType.BreakStart, 2026, 8, 31, 12, 0),
            CreateEvent(AttendanceEventType.BreakEnd, 2026, 8, 31, 12, 20, isInsideGeofence: false),
            CreateEvent(AttendanceEventType.ClockOut, 2026, 8, 31, 16, 0),
            CreateEvent(AttendanceEventType.ClockIn, 2026, 9, 1, 9, 0)
        ]);
        var service = CreateService(store);

        var result = await service.GetDayAsync(
            new DateOnly(2026, 8, 31),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-08-31", result.Value?.Date);
        Assert.Equal(460, result.Value?.WorkedMinutes);
        Assert.Equal(20, result.Value?.BreakMinutes);
        Assert.True(result.Value?.HasOutsideGeofence);
        Assert.Single(result.Value!.Breaks);
        Assert.Equal(4, result.Value.Events.Count);
        Assert.Equal("ClockIn", result.Value.Events[0].EventType);
        Assert.Equal("ClockOut", result.Value.Events[^1].EventType);
    }

    [Fact]
    public async Task GetDayAsync_DoesNotAccrueMissingHistoricalClockOutBeyondLastEvent()
    {
        var store = new FakeAttendanceStore();
        store.Events.Add(
            CreateEvent(AttendanceEventType.ClockIn, 2026, 8, 31, 8, 0));
        var service = CreateService(store);

        var result = await service.GetDayAsync(
            new DateOnly(2026, 8, 31),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value?.WorkedMinutes);
        Assert.Null(result.Value?.ClockOut);
    }

    [Fact]
    public async Task GetDayAsync_ReturnsEmptyDetailForDayWithoutEvents()
    {
        var service = CreateService(new FakeAttendanceStore());

        var result = await service.GetDayAsync(
            new DateOnly(2026, 8, 30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-08-30", result.Value?.Date);
        Assert.Empty(result.Value!.Events);
        Assert.Empty(result.Value.Breaks);
        Assert.Equal("NoRecord", result.Value.CurrentStatus);
    }

    private static AttendanceService CreateService(FakeAttendanceStore store)
    {
        return new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new FakeCurrentUserProvider(UserId, EmployeeId),
            new FakeGeolocationService(),
            new FixedTimeProvider(Now));
    }

    private static AttendanceEvent CreateEvent(
        AttendanceEventType eventType,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        bool? isInsideGeofence = true)
    {
        var timestamp = new DateTimeOffset(
            year,
            month,
            day,
            hour,
            minute,
            0,
            TimeSpan.Zero);

        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            EmployeeId = EmployeeId,
            EventType = eventType,
            ServerTimestampUtc = timestamp,
            ClientEventId = Guid.NewGuid(),
            IsInsideGeofence = isInsideGeofence,
            CreatedAtUtc = timestamp
        };
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public FakeCurrentCompanyProvider(Guid? companyId) => CompanyId = companyId;
        public Guid? CompanyId { get; }
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        public FakeCurrentUserProvider(Guid? userId, Guid? employeeId)
        {
            UserId = userId;
            EmployeeId = employeeId;
        }

        public Guid? UserId { get; }
        public Guid? EmployeeId { get; }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;
        public FixedTimeProvider(DateTimeOffset now) => this.now = now;
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                GeolocationResult<GeolocationValidationDto>.Success(
                    new GeolocationValidationDto(
                        true,
                        true,
                        0,
                        GeofenceMode.Disabled,
                        "GeofenceDisabled",
                        "Geofence desativada.")));
        }
    }

    private sealed class FakeAttendanceStore : IAttendanceStore
    {
        public List<AttendanceEvent> Events { get; } = [];

        public Task<bool> EmployeeCanPunchAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> ProjectExistsAsync(
            Guid companyId,
            Guid projectId,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<AttendanceEvent?> GetByClientEventIdAsync(
            Guid companyId,
            Guid employeeId,
            Guid clientEventId,
            CancellationToken cancellationToken) => Task.FromResult<AttendanceEvent?>(null);

        public Task<AttendanceEvent?> GetEventAsync(
            Guid companyId,
            Guid attendanceEventId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Events.SingleOrDefault(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.Id == attendanceEventId));
        }

        public Task<AttendanceEventType?> GetLastEventTypeAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Events.OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
                    .Select(attendanceEvent => (AttendanceEventType?)attendanceEvent.EventType)
                    .LastOrDefault());
        }

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<AttendanceEmployeeStateReference?>(
                new AttendanceEmployeeStateReference(
                    EmployeeId,
                    "Funcionario Demo",
                    "UTC"));
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
                companyId == CompanyId && (!employeeId.HasValue || employeeId.Value == EmployeeId)
                    ? [new AttendanceBackofficeEmployeeReference(
                        EmployeeId,
                        "FUNC001",
                        "Funcionario Demo",
                        null,
                        null)]
                    : [];

            return Task.FromResult(employees);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
            Guid companyId,
            Guid employeeId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceEvent> result = Events
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.EmployeeId == employeeId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc)
                .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
            Guid companyId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceEvent> result = Events
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc
                    && attendanceEvent.ServerTimestampUtc < toUtc
                    && (!employeeId.HasValue || attendanceEvent.EmployeeId == employeeId.Value))
                .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<AttendanceEventCorrectionReference>> GetCorrectionsForEventsAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> attendanceEventIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AttendanceEventCorrectionReference>>([]);
        }

        public void Add(AttendanceEvent attendanceEvent) => Events.Add(attendanceEvent);
        public void Add(AttendanceCorrection attendanceCorrection) { }
        public void Add(AuditLog auditLog) { }
        public void Add(IntegrationOutbox integrationOutbox) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
