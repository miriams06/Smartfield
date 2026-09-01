using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class AttendanceTodayServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid UserId = Guid.Parse("4a290c06-2a4b-4f22-a2df-76111c8d055b");
    private static readonly Guid EmployeeId = Guid.Parse("70bfeaba-236d-48b0-b9ab-a3f8cb22d389");
    private static readonly DateTimeOffset ServerNow = new(2026, 9, 1, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetTodayAsync_WithoutEventsReturnsEmptyDayAndClockInAsNextAction()
    {
        var service = CreateService(new FakeAttendanceStore());

        var result = await service.GetTodayAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value?.ClockIn);
        Assert.Null(result.Value?.ClockOut);
        Assert.Empty(result.Value!.Breaks);
        Assert.Equal(0, result.Value.WorkedMinutes);
        Assert.Equal(0, result.Value.BreakMinutes);
        Assert.Equal("NoRecord", result.Value.CurrentStatus);
        Assert.Equal(["ClockIn"], result.Value.NextAllowedActions);
        Assert.Empty(result.Value.Events);
    }

    [Fact]
    public async Task GetTodayAsync_CalculatesWorkedTimeAndMultipleBreaks()
    {
        var store = new FakeAttendanceStore();
        store.Events.AddRange(
        [
            CreateEvent(AttendanceEventType.ClockIn, 8, 0),
            CreateEvent(AttendanceEventType.BreakStart, 12, 0),
            CreateEvent(AttendanceEventType.BreakEnd, 12, 30),
            CreateEvent(AttendanceEventType.BreakStart, 15, 0),
            CreateEvent(AttendanceEventType.BreakEnd, 15, 15),
            CreateEvent(AttendanceEventType.ClockOut, 17, 0)
        ]);
        var service = CreateService(store);

        var result = await service.GetTodayAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), result.Value?.ClockIn);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 17, 0, 0, TimeSpan.Zero), result.Value?.ClockOut);
        Assert.Equal(495, result.Value?.WorkedMinutes);
        Assert.Equal(45, result.Value?.BreakMinutes);
        Assert.Equal("Closed", result.Value?.CurrentStatus);
        Assert.Equal(["ClockIn"], result.Value?.NextAllowedActions);
        Assert.Equal(2, result.Value?.Breaks.Count);
        Assert.Equal(30, result.Value?.Breaks[0].Minutes);
        Assert.Equal(15, result.Value?.Breaks[1].Minutes);
        Assert.Equal(6, result.Value?.Events.Count);
        Assert.Equal("ClockIn", result.Value?.Events[0].EventType);
        Assert.Equal("ClockOut", result.Value?.Events[^1].EventType);
    }

    [Fact]
    public async Task GetTodayAsync_OpenBreakCountsUntilServerNow()
    {
        var store = new FakeAttendanceStore();
        store.Events.AddRange(
        [
            CreateEvent(AttendanceEventType.ClockIn, 8, 0),
            CreateEvent(AttendanceEventType.BreakStart, 12, 0)
        ]);
        var service = CreateService(store);

        var result = await service.GetTodayAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(240, result.Value?.WorkedMinutes);
        Assert.Equal(300, result.Value?.BreakMinutes);
        Assert.Equal("OnBreak", result.Value?.CurrentStatus);
        Assert.Equal(["BreakEnd"], result.Value?.NextAllowedActions);
        var attendanceBreak = Assert.Single(result.Value!.Breaks);
        Assert.Null(attendanceBreak.EndedAtUtc);
        Assert.Equal(300, attendanceBreak.Minutes);
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

    private static AttendanceEvent CreateEvent(AttendanceEventType eventType, int hour, int minute)
    {
        var timestamp = new DateTimeOffset(2026, 9, 1, hour, minute, 0, TimeSpan.Zero);
        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            EmployeeId = EmployeeId,
            EventType = eventType,
            ServerTimestampUtc = timestamp,
            CreatedAtUtc = timestamp,
            ClientEventId = Guid.NewGuid()
        };
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public Guid? CompanyId => AttendanceTodayServiceTests.CompanyId;
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? UserId => AttendanceTodayServiceTests.UserId;
        public Guid? EmployeeId => AttendanceTodayServiceTests.EmployeeId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ServerNow;
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
        public List<AttendanceEvent> Events { get; } = [];

        public Task<bool> EmployeeCanPunchAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
            => Task.FromResult(companyId == CompanyId && employeeId == EmployeeId);

        public Task<bool> ProjectExistsAsync(Guid companyId, Guid projectId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<AttendanceEvent?> GetByClientEventIdAsync(Guid companyId, Guid employeeId, Guid clientEventId, CancellationToken cancellationToken)
            => Task.FromResult<AttendanceEvent?>(null);

        public Task<AttendanceEvent?> GetEventAsync(Guid companyId, Guid attendanceEventId, CancellationToken cancellationToken)
            => Task.FromResult(Events.SingleOrDefault(item => item.CompanyId == companyId && item.Id == attendanceEventId));

        public Task<AttendanceEventType?> GetLastEventTypeAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
            => Task.FromResult(Events.OrderBy(item => item.ServerTimestampUtc).Select(item => (AttendanceEventType?)item.EventType).LastOrDefault());

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(Guid companyId, Guid employeeId, CancellationToken cancellationToken)
            => Task.FromResult<AttendanceEmployeeStateReference?>(new AttendanceEmployeeStateReference(EmployeeId, "Funcionario Demo", "UTC"));

        public Task<string?> GetCompanyTimeZoneAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult<string?>(companyId == CompanyId ? "UTC" : null);

        public Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
            Guid companyId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceBackofficeEmployeeReference> employees =
                companyId == CompanyId && (!employeeId.HasValue || employeeId.Value == EmployeeId)
                    ? [new AttendanceBackofficeEmployeeReference(EmployeeId, "FUNC001", "Funcionario Demo", null, null)]
                    : [];

            return Task.FromResult(employees);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(Guid companyId, Guid employeeId, DateTimeOffset fromUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AttendanceEvent>>(Events.Where(item => item.ServerTimestampUtc >= fromUtc).ToArray());

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
            Guid companyId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceEvent> events = Events
                .Where(item =>
                    item.CompanyId == companyId
                    && item.ServerTimestampUtc >= fromUtc
                    && item.ServerTimestampUtc < toUtc
                    && (!employeeId.HasValue || item.EmployeeId == employeeId.Value))
                .ToArray();

            return Task.FromResult(events);
        }

        public Task<IReadOnlyList<AttendanceEventCorrectionReference>> GetCorrectionsForEventsAsync(
            Guid companyId,
            IReadOnlyCollection<Guid> attendanceEventIds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<AttendanceEventCorrectionReference>>([]);
        }

        public void Add(AttendanceEvent attendanceEvent) => throw new NotSupportedException();
        public void Add(AttendanceCorrection attendanceCorrection) => throw new NotSupportedException();
        public void Add(AuditLog auditLog) => throw new NotSupportedException();
        public void Add(IntegrationOutbox integrationOutbox) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
