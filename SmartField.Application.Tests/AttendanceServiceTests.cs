using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Tests;

public class AttendanceServiceTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid UserId =
        Guid.Parse("4a290c06-2a4b-4f22-a2df-76111c8d055b");
    private static readonly Guid EmployeeId =
        Guid.Parse("70bfeaba-236d-48b0-b9ab-a3f8cb22d389");
    private static readonly Guid WorkSiteId =
        Guid.Parse("cb9ed2c6-69e8-4b85-9ea5-52b496a31f11");
    private static readonly Guid ProjectId =
        Guid.Parse("c6bb29ae-f681-4ff7-8a9e-c9a2689bd319");
    private static readonly DateTimeOffset ServerNow =
        new(2026, 8, 31, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetStateAsync_ReturnsAllowedNextOperations()
    {
        var store = new FakeAttendanceStore
        {
            LastEventType = AttendanceEventType.ClockIn
        };
        var service = CreateService(store);

        var result = await service.GetStateAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeId, result.Value?.EmployeeId);
        Assert.Equal("Funcionario Demo", result.Value?.EmployeeName);
        Assert.Equal("Working", result.Value?.CurrentState);
        Assert.Equal("EM TRABALHO", result.Value?.CurrentStateLabel);
        Assert.Equal("2026-08-31", result.Value?.LocalDate);
        Assert.Equal("ClockIn", result.Value?.LastEventType);
        Assert.Equal(
            ["BreakStart", "ClockOut"],
            result.Value?.AllowedEventTypes);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsDailyDurationsEntryAndBreaks()
    {
        var store = new FakeAttendanceStore
        {
            LastEventType = AttendanceEventType.BreakStart
        };
        store.StateEvents.AddRange(
        [
            CreateExistingEvent(
                Guid.NewGuid(),
                AttendanceEventType.ClockIn,
                new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero)),
            CreateExistingEvent(
                Guid.NewGuid(),
                AttendanceEventType.BreakStart,
                new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero)),
            CreateExistingEvent(
                Guid.NewGuid(),
                AttendanceEventType.BreakEnd,
                new DateTimeOffset(2026, 8, 31, 12, 30, 0, TimeSpan.Zero)),
            CreateExistingEvent(
                Guid.NewGuid(),
                AttendanceEventType.BreakStart,
                new DateTimeOffset(2026, 8, 31, 16, 0, 0, TimeSpan.Zero))
        ]);
        var service = CreateService(store);

        var result = await service.GetStateAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("OnBreak", result.Value?.CurrentState);
        Assert.Equal(new DateTimeOffset(2026, 8, 31, 8, 0, 0, TimeSpan.Zero), result.Value?.ClockInAtUtc);
        Assert.Equal(450, result.Value?.WorkedDurationMinutes);
        Assert.Equal(90, result.Value?.BreakDurationMinutes);
        Assert.Equal(2, result.Value?.BreakCount);
        Assert.Equal(["BreakEnd"], result.Value?.AllowedEventTypes);
    }

    [Theory]
    [InlineData(null, "ClockIn")]
    [InlineData(AttendanceEventType.ClockIn, "BreakStart")]
    [InlineData(AttendanceEventType.BreakStart, "BreakEnd")]
    [InlineData(AttendanceEventType.BreakEnd, "ClockOut")]
    [InlineData(AttendanceEventType.ClockIn, "ClockOut")]
    [InlineData(AttendanceEventType.BreakEnd, "BreakStart")]
    [InlineData(AttendanceEventType.ClockOut, "ClockIn")]
    public async Task PunchAsync_AcceptsValidSequences(
        AttendanceEventType? previous,
        string next)
    {
        var store = new FakeAttendanceStore { LastEventType = previous };
        var service = CreateService(store);

        var result = await service.PunchAsync(CreateRequest(next), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value?.IsDuplicate);
        Assert.Equal(next, result.Value?.EventType);
        Assert.Single(store.AttendanceEvents);
        Assert.Single(store.AuditLogs);
        Assert.Single(store.OutboxItems);
    }

    [Fact]
    public async Task PunchAsync_UsesServerTimeAndStoresClientTimeAsAdditionalInformation()
    {
        var store = new FakeAttendanceStore();
        var service = CreateService(store);
        var clientTimestamp = new DateTimeOffset(
            2026, 8, 31, 18, 15, 0, TimeSpan.FromHours(1));

        var result = await service.PunchAsync(
            CreateRequest("ClockIn") with { ClientTimestampUtc = clientTimestamp },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendanceEvent = Assert.Single(store.AttendanceEvents);
        Assert.Equal(ServerNow, attendanceEvent.ServerTimestampUtc);
        Assert.Equal(ServerNow, attendanceEvent.CreatedAtUtc);
        Assert.Equal(clientTimestamp.ToUniversalTime(), attendanceEvent.ClientTimestampUtc);
        Assert.NotEqual(attendanceEvent.ClientTimestampUtc, attendanceEvent.ServerTimestampUtc);
    }

    [Fact]
    public async Task PunchAsync_PersistsGeofenceAuditAndOutboxData()
    {
        var store = new FakeAttendanceStore();
        var geolocation = new FakeGeolocationService
        {
            Result = GeolocationResult<GeolocationValidationDto>.Success(
                new GeolocationValidationDto(
                    true,
                    true,
                    12.34m,
                    GeofenceMode.Block,
                    "InsideGeofence",
                    "Dentro do raio."))
        };
        var service = CreateService(store, geolocation);

        var result = await service.PunchAsync(
            CreateRequest("ClockIn") with { ProjectId = ProjectId },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var attendanceEvent = Assert.Single(store.AttendanceEvents);
        Assert.Equal(CompanyId, attendanceEvent.CompanyId);
        Assert.Equal(EmployeeId, attendanceEvent.EmployeeId);
        Assert.Equal(WorkSiteId, attendanceEvent.WorkSiteId);
        Assert.Equal(ProjectId, attendanceEvent.ProjectId);
        Assert.True(attendanceEvent.IsInsideGeofence);
        Assert.Equal(12.34m, attendanceEvent.DistanceFromWorkSiteMeters);

        var audit = Assert.Single(store.AuditLogs);
        Assert.Equal(UserId, audit.UserId);
        Assert.Equal(attendanceEvent.Id, audit.EntityId);
        Assert.Equal("AttendanceEvent", audit.EntityType);
        Assert.Equal("Created", audit.Action);
        Assert.Contains(attendanceEvent.ClientEventId.ToString(), audit.NewValues);

        var outbox = Assert.Single(store.OutboxItems);
        Assert.Equal(attendanceEvent.Id, outbox.EntityId);
        Assert.Equal("AttendanceEventCreated", outbox.EventType);
        Assert.Equal("AttendanceEvent", outbox.EntityType);
        Assert.Contains(attendanceEvent.ClientEventId.ToString(), outbox.Payload);
        Assert.Equal(1, store.SaveChangesCalls);
    }

    [Fact]
    public async Task PunchAsync_ReturnsExistingEventForDuplicateClientEventId()
    {
        var request = CreateRequest("ClockIn");
        var existing = CreateExistingEvent(request.ClientEventId);
        var store = new FakeAttendanceStore { Existing = existing };
        var service = CreateService(store);

        var result = await service.PunchAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsDuplicate);
        Assert.Equal(existing.Id, result.Value?.Id);
        Assert.Empty(store.AttendanceEvents);
        Assert.Empty(store.AuditLogs);
        Assert.Empty(store.OutboxItems);
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Fact]
    public async Task PunchAsync_RecoversIdempotentlyFromConcurrentDuplicate()
    {
        var request = CreateRequest("ClockIn");
        var existing = CreateExistingEvent(request.ClientEventId);
        var store = new FakeAttendanceStore
        {
            ThrowClientEventConflictOnSave = true,
            ExistingAfterSaveConflict = existing
        };
        var service = CreateService(store);

        var result = await service.PunchAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsDuplicate);
        Assert.Equal(existing.Id, result.Value?.Id);
    }

    [Fact]
    public async Task PunchAsync_RejectsInvalidSequenceWithoutWriting()
    {
        var store = new FakeAttendanceStore
        {
            LastEventType = AttendanceEventType.BreakStart
        };
        var service = CreateService(store);

        var result = await service.PunchAsync(
            CreateRequest("ClockOut"),
            CancellationToken.None);

        Assert.Equal(AttendanceError.InvalidSequence, result.Error);
        Assert.Empty(store.AttendanceEvents);
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Theory]
    [InlineData(null, "ClockOut")]
    [InlineData(null, "BreakStart")]
    [InlineData(AttendanceEventType.ClockIn, "BreakEnd")]
    [InlineData(AttendanceEventType.ClockIn, "ClockIn")]
    public async Task PunchAsync_RejectsPlannerInvalidSequences(
        AttendanceEventType? previous,
        string next)
    {
        var store = new FakeAttendanceStore
        {
            LastEventType = previous
        };
        var service = CreateService(store);

        var result = await service.PunchAsync(
            CreateRequest(next),
            CancellationToken.None);

        Assert.Equal(AttendanceError.InvalidSequence, result.Error);
        Assert.Empty(store.AttendanceEvents);
        Assert.Empty(store.AuditLogs);
        Assert.Empty(store.OutboxItems);
        Assert.Equal(0, store.SaveChangesCalls);
    }

    [Fact]
    public async Task PunchAsync_RejectsInactiveOrForeignEmployee()
    {
        var store = new FakeAttendanceStore { EmployeeCanPunch = false };
        var service = CreateService(store);

        var result = await service.PunchAsync(
            CreateRequest("ClockIn"),
            CancellationToken.None);

        Assert.Equal(AttendanceError.EmployeeUnavailable, result.Error);
        Assert.Empty(store.AttendanceEvents);
    }

    [Fact]
    public async Task PunchAsync_RejectsProjectOutsideAuthenticatedCompany()
    {
        var store = new FakeAttendanceStore { ProjectExists = false };
        var service = CreateService(store);

        var result = await service.PunchAsync(
            CreateRequest("ClockIn") with { ProjectId = ProjectId },
            CancellationToken.None);

        Assert.Equal(AttendanceError.ProjectNotFound, result.Error);
        Assert.Empty(store.AttendanceEvents);
    }

    [Fact]
    public async Task PunchAsync_RejectsWhenGeofenceBlocksPunch()
    {
        var geolocation = new FakeGeolocationService
        {
            Result = GeolocationResult<GeolocationValidationDto>.Success(
                new GeolocationValidationDto(
                    false,
                    false,
                    250,
                    GeofenceMode.Block,
                    "OutsideGeofenceBlocked",
                    "Fora do raio permitido."))
        };
        var store = new FakeAttendanceStore();
        var service = CreateService(store, geolocation);

        var result = await service.PunchAsync(
            CreateRequest("ClockIn"),
            CancellationToken.None);

        Assert.Equal(AttendanceError.GeofenceRejected, result.Error);
        Assert.Equal("Fora do raio permitido.", result.Detail);
        Assert.Empty(store.AttendanceEvents);
    }

    [Fact]
    public async Task PunchAsync_ReturnsValidationForInvalidEventAndClientEventId()
    {
        var store = new FakeAttendanceStore();
        var service = CreateService(store);

        var result = await service.PunchAsync(
            CreateRequest("Invalid") with { ClientEventId = Guid.Empty },
            CancellationToken.None);

        Assert.Equal(AttendanceError.Validation, result.Error);
        Assert.Contains(nameof(AttendancePunchRequest.EventType), result.ValidationErrors.Keys);
        Assert.Contains(nameof(AttendancePunchRequest.ClientEventId), result.ValidationErrors.Keys);
        Assert.Empty(store.AttendanceEvents);
    }

    [Fact]
    public async Task PunchAsync_ReturnsCompanyUnavailableWithoutAuthenticatedCompany()
    {
        var store = new FakeAttendanceStore();
        var service = new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(null),
            new FakeCurrentUserProvider(UserId, EmployeeId),
            new FakeGeolocationService(),
            new FixedTimeProvider(ServerNow));

        var result = await service.PunchAsync(
            CreateRequest("ClockIn"),
            CancellationToken.None);

        Assert.Equal(AttendanceError.CompanyUnavailable, result.Error);
        Assert.Empty(store.AttendanceEvents);
    }

    [Fact]
    public async Task PunchAsync_ReturnsEmployeeUnavailableWithoutEmployeeClaim()
    {
        var store = new FakeAttendanceStore();
        var service = new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new FakeCurrentUserProvider(UserId, null),
            new FakeGeolocationService(),
            new FixedTimeProvider(ServerNow));

        var result = await service.PunchAsync(
            CreateRequest("ClockIn"),
            CancellationToken.None);

        Assert.Equal(AttendanceError.EmployeeUnavailable, result.Error);
        Assert.Empty(store.AttendanceEvents);
    }

    private static AttendanceService CreateService(
        FakeAttendanceStore store,
        FakeGeolocationService? geolocation = null)
    {
        return new AttendanceService(
            store,
            new FakeCurrentCompanyProvider(CompanyId),
            new FakeCurrentUserProvider(UserId, EmployeeId),
            geolocation ?? new FakeGeolocationService(),
            new FixedTimeProvider(ServerNow));
    }

    private static AttendancePunchRequest CreateRequest(string eventType)
    {
        return new AttendancePunchRequest(
            eventType,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 8, 31, 16, 59, 0, TimeSpan.Zero),
            38.722252m,
            -9.139337m,
            10,
            WorkSiteId,
            null);
    }

    private static AttendanceEvent CreateExistingEvent(
        Guid clientEventId,
        AttendanceEventType eventType = AttendanceEventType.ClockIn,
        DateTimeOffset? serverTimestampUtc = null)
    {
        return new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            EmployeeId = EmployeeId,
            EventType = eventType,
            ClientEventId = clientEventId,
            ServerTimestampUtc = serverTimestampUtc ?? ServerNow,
            CreatedAtUtc = serverTimestampUtc ?? ServerNow
        };
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public FakeCurrentCompanyProvider(Guid? companyId)
        {
            CompanyId = companyId;
        }

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
        private readonly DateTimeOffset utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeGeolocationService : IGeolocationService
    {
        public GeolocationResult<GeolocationValidationDto> Result { get; set; } =
            GeolocationResult<GeolocationValidationDto>.Success(
                new GeolocationValidationDto(
                    true,
                    true,
                    5,
                    GeofenceMode.Warning,
                    "InsideGeofence",
                    "Dentro do raio."));

        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAttendanceStore : IAttendanceStore
    {
        private int getByClientEventCalls;

        public bool EmployeeCanPunch { get; set; } = true;

        public bool ProjectExists { get; set; } = true;

        public AttendanceEvent? Existing { get; set; }

        public AttendanceEvent? ExistingAfterSaveConflict { get; set; }

        public AttendanceEventType? LastEventType { get; set; }

        public List<AttendanceEvent> StateEvents { get; } = [];

        public string? CompanyTimeZone { get; set; } = "Europe/Lisbon";

        public List<AttendanceBackofficeEmployeeReference> BackofficeEmployees { get; } = [];

        public bool ThrowClientEventConflictOnSave { get; set; }

        public int SaveChangesCalls { get; private set; }

        public List<AttendanceEvent> AttendanceEvents { get; } = [];

        public List<AuditLog> AuditLogs { get; } = [];

        public List<IntegrationOutbox> OutboxItems { get; } = [];

        public Task<bool> EmployeeCanPunchAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                EmployeeCanPunch
                && companyId == CompanyId
                && employeeId == EmployeeId);
        }

        public Task<bool> ProjectExistsAsync(
            Guid companyId,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                ProjectExists
                && companyId == CompanyId
                && projectId == ProjectId);
        }

        public Task<AttendanceEvent?> GetByClientEventIdAsync(
            Guid companyId,
            Guid employeeId,
            Guid clientEventId,
            CancellationToken cancellationToken)
        {
            getByClientEventCalls++;
            var value = getByClientEventCalls > 1 && ExistingAfterSaveConflict is not null
                ? ExistingAfterSaveConflict
                : Existing;
            return Task.FromResult(value);
        }

        public Task<AttendanceEventType?> GetLastEventTypeAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(LastEventType);
        }

        public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
            Guid companyId,
            Guid employeeId,
            CancellationToken cancellationToken)
        {
            AttendanceEmployeeStateReference? employee =
                companyId == CompanyId && employeeId == EmployeeId
                    ? new AttendanceEmployeeStateReference(
                        EmployeeId,
                        "Funcionario Demo",
                        "Europe/Lisbon")
                    : null;

            return Task.FromResult(employee);
        }

        public Task<string?> GetCompanyTimeZoneAsync(
            Guid companyId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId ? CompanyTimeZone : null);
        }

        public Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
            Guid companyId,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AttendanceBackofficeEmployeeReference> employees =
                BackofficeEmployees.Count > 0
                    ? BackofficeEmployees
                    : [new AttendanceBackofficeEmployeeReference(
                        EmployeeId,
                        "FUNC001",
                        "Funcionario Demo",
                        WorkSiteId,
                        "Sede")];

            employees = employees
                .Where(employee =>
                    companyId == CompanyId
                    && (!employeeId.HasValue || employee.EmployeeId == employeeId.Value))
                .ToArray();

            return Task.FromResult(employees);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
            Guid companyId,
            Guid employeeId,
            DateTimeOffset fromUtc,
            CancellationToken cancellationToken)
        {
            var events = StateEvents
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.EmployeeId == employeeId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc)
                .ToArray();

            return Task.FromResult<IReadOnlyList<AttendanceEvent>>(events);
        }

        public Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
            Guid companyId,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            Guid? employeeId,
            CancellationToken cancellationToken)
        {
            var events = StateEvents
                .Where(attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.ServerTimestampUtc >= fromUtc
                    && attendanceEvent.ServerTimestampUtc < toUtc
                    && (!employeeId.HasValue || attendanceEvent.EmployeeId == employeeId.Value))
                .ToArray();

            return Task.FromResult<IReadOnlyList<AttendanceEvent>>(events);
        }

        public void Add(AttendanceEvent attendanceEvent)
        {
            AttendanceEvents.Add(attendanceEvent);
        }

        public void Add(AuditLog auditLog)
        {
            AuditLogs.Add(auditLog);
        }

        public void Add(IntegrationOutbox integrationOutbox)
        {
            OutboxItems.Add(integrationOutbox);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            if (ThrowClientEventConflictOnSave)
            {
                throw new AttendanceClientEventConflictException(
                    "duplicate",
                    new InvalidOperationException("duplicate"));
            }

            return Task.CompletedTask;
        }
    }
}
