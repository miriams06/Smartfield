using System.Text.Json;
using SmartField.Application.Abstractions;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Attendance;

public sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceStore attendanceStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IGeolocationService geolocationService;
    private readonly TimeProvider timeProvider;

    public AttendanceService(
        IAttendanceStore attendanceStore,
        ICurrentCompanyProvider currentCompanyProvider,
        ICurrentUserProvider currentUserProvider,
        IGeolocationService geolocationService,
        TimeProvider timeProvider)
    {
        this.attendanceStore = attendanceStore;
        this.currentCompanyProvider = currentCompanyProvider;
        this.currentUserProvider = currentUserProvider;
        this.geolocationService = geolocationService;
        this.timeProvider = timeProvider;
    }

    public async Task<AttendanceResult<AttendanceStateDto>> GetStateAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetAttendanceContextAsync(cancellationToken);
        if (!context.IsSuccess)
        {
            return AttendanceResult<AttendanceStateDto>.Failure(context.Error);
        }

        var lastEventType = await attendanceStore.GetLastEventTypeAsync(
            context.CompanyId,
            context.EmployeeId,
            cancellationToken);

        var employee = await attendanceStore.GetEmployeeStateReferenceAsync(
            context.CompanyId,
            context.EmployeeId,
            cancellationToken);

        if (employee is null)
        {
            return AttendanceResult<AttendanceStateDto>.Failure(
                AttendanceError.EmployeeUnavailable);
        }

        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyTimeZone = GetCompanyTimeZone(employee.CompanyTimeZone);
        var companyNow = TimeZoneInfo.ConvertTime(calculatedAtUtc, companyTimeZone);
        var localMidnight = new DateTimeOffset(
            companyNow.Date,
            companyNow.Offset);
        var todayEvents = await attendanceStore.GetEventsFromAsync(
            context.CompanyId,
            context.EmployeeId,
            localMidnight.ToUniversalTime(),
            cancellationToken);

        return AttendanceResult<AttendanceStateDto>.Success(
            BuildState(
                employee,
                lastEventType,
                todayEvents,
                companyNow.Date,
                calculatedAtUtc));
    }

    public async Task<AttendanceResult<AttendanceTodayDto>> GetTodayAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetAttendanceContextAsync(cancellationToken);
        if (!context.IsSuccess)
        {
            return AttendanceResult<AttendanceTodayDto>.Failure(context.Error);
        }

        var employee = await attendanceStore.GetEmployeeStateReferenceAsync(
            context.CompanyId,
            context.EmployeeId,
            cancellationToken);

        if (employee is null)
        {
            return AttendanceResult<AttendanceTodayDto>.Failure(
                AttendanceError.EmployeeUnavailable);
        }

        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyTimeZone = GetCompanyTimeZone(employee.CompanyTimeZone);
        var companyNow = TimeZoneInfo.ConvertTime(calculatedAtUtc, companyTimeZone);
        var localMidnight = new DateTimeOffset(companyNow.Date, companyNow.Offset);
        var nextLocalMidnight = localMidnight.AddDays(1);

        var loadedEvents = await attendanceStore.GetEventsFromAsync(
            context.CompanyId,
            context.EmployeeId,
            localMidnight.ToUniversalTime(),
            cancellationToken);
        var todayEvents = loadedEvents
            .Where(attendanceEvent =>
                attendanceEvent.ServerTimestampUtc < nextLocalMidnight.ToUniversalTime())
            .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenBy(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenBy(attendanceEvent => attendanceEvent.Id)
            .ToArray();

        var metrics = CalculateDailyMetrics(todayEvents, calculatedAtUtc);
        var lastEventType = todayEvents.LastOrDefault()?.EventType;
        var clockOut = todayEvents
            .Where(attendanceEvent => attendanceEvent.EventType == AttendanceEventType.ClockOut)
            .Select(attendanceEvent => (DateTimeOffset?)attendanceEvent.ServerTimestampUtc)
            .LastOrDefault();

        return AttendanceResult<AttendanceTodayDto>.Success(
            new AttendanceTodayDto(
                metrics.ClockInAtUtc,
                clockOut,
                BuildBreaks(todayEvents, calculatedAtUtc),
                metrics.WorkedDurationMinutes,
                metrics.BreakDurationMinutes,
                AttendanceSequenceRules.GetCurrentState(lastEventType),
                AttendanceSequenceRules
                    .GetAllowedNextEventTypes(lastEventType)
                    .Select(eventType => eventType.ToString())
                    .ToArray(),
                todayEvents.Select(MapTodayEvent).ToArray()));
    }

    public async Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        var context = await GetAttendanceContextAsync(cancellationToken);
        if (!context.IsSuccess)
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                context.Error);
        }

        var validation = ValidateRequest(request);
        if (validation.Errors.Count > 0)
        {
            return AttendanceResult<AttendancePunchDto>.Invalid(validation.Errors);
        }

        var existing = await attendanceStore.GetByClientEventIdAsync(
            context.CompanyId,
            context.EmployeeId,
            request.ClientEventId,
            cancellationToken);

        if (existing is not null)
        {
            return AttendanceResult<AttendancePunchDto>.Success(
                Map(existing, isDuplicate: true));
        }

        if (request.ProjectId.HasValue
            && !await attendanceStore.ProjectExistsAsync(
                context.CompanyId,
                request.ProjectId.Value,
                cancellationToken))
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.ProjectNotFound);
        }

        var geolocationResult = await geolocationService.ValidateAsync(
            new GeolocationValidationRequest(
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                request.WorkSiteId),
            cancellationToken);

        if (!geolocationResult.IsSuccess)
        {
            return geolocationResult.Error switch
            {
                GeolocationError.CompanyUnavailable =>
                    AttendanceResult<AttendancePunchDto>.Failure(
                        AttendanceError.CompanyUnavailable),
                GeolocationError.Validation =>
                    AttendanceResult<AttendancePunchDto>.Invalid(
                        geolocationResult.ValidationErrors),
                GeolocationError.WorkSiteNotFound =>
                    AttendanceResult<AttendancePunchDto>.Failure(
                        AttendanceError.WorkSiteNotFound),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(geolocationResult.Error),
                    geolocationResult.Error,
                    null)
            };
        }

        var geolocation = geolocationResult.Value!;
        if (!geolocation.IsAccepted)
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.GeofenceRejected,
                geolocation.Message);
        }

        var lastEventType = await attendanceStore.GetLastEventTypeAsync(
            context.CompanyId,
            context.EmployeeId,
            cancellationToken);

        if (!AttendanceSequenceRules.IsAllowed(lastEventType, validation.EventType))
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.InvalidSequence,
                AttendanceSequenceRules.BuildSequenceError(
                    lastEventType,
                    validation.EventType));
        }

        var serverTimestampUtc = timeProvider.GetUtcNow();
        var attendanceEvent = new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = context.CompanyId,
            EmployeeId = context.EmployeeId,
            EventType = validation.EventType,
            ServerTimestampUtc = serverTimestampUtc,
            ClientTimestampUtc = request.ClientTimestampUtc?.ToUniversalTime(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationAccuracyMeters = request.AccuracyMeters,
            WorkSiteId = request.WorkSiteId,
            ProjectId = request.ProjectId,
            IsInsideGeofence = geolocation.IsInsideGeofence,
            DistanceFromWorkSiteMeters = geolocation.DistanceFromWorkSiteMeters,
            Source = AttendanceSource.PWA,
            ClientEventId = request.ClientEventId,
            CreatedAtUtc = serverTimestampUtc
        };

        var payload = SerializeEvent(attendanceEvent);

        attendanceStore.Add(attendanceEvent);
        attendanceStore.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = context.CompanyId,
            UserId = context.UserId,
            EntityType = nameof(AttendanceEvent),
            EntityId = attendanceEvent.Id,
            Action = "Created",
            NewValues = payload,
            TimestampUtc = serverTimestampUtc,
            CreatedAtUtc = serverTimestampUtc
        });
        attendanceStore.Add(new IntegrationOutbox
        {
            Id = Guid.NewGuid(),
            CompanyId = context.CompanyId,
            EventType = "AttendanceEventCreated",
            EntityType = nameof(AttendanceEvent),
            EntityId = attendanceEvent.Id,
            Payload = payload,
            CreatedAtUtc = serverTimestampUtc
        });

        try
        {
            await attendanceStore.SaveChangesAsync(cancellationToken);
        }
        catch (AttendanceClientEventConflictException)
        {
            existing = await attendanceStore.GetByClientEventIdAsync(
                context.CompanyId,
                context.EmployeeId,
                request.ClientEventId,
                cancellationToken);

            return existing is not null
                ? AttendanceResult<AttendancePunchDto>.Success(
                    Map(existing, isDuplicate: true))
                : AttendanceResult<AttendancePunchDto>.Failure(
                    AttendanceError.ClientEventConflict);
        }

        return AttendanceResult<AttendancePunchDto>.Success(
            Map(attendanceEvent, isDuplicate: false));
    }

    private async Task<AttendanceContext> GetAttendanceContextAsync(
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return AttendanceContext.Failure(AttendanceError.CompanyUnavailable);
        }

        var userId = currentUserProvider.UserId;
        if (!userId.HasValue)
        {
            return AttendanceContext.Failure(AttendanceError.UserUnavailable);
        }

        var employeeId = currentUserProvider.EmployeeId;
        if (!employeeId.HasValue
            || !await attendanceStore.EmployeeCanPunchAsync(
                companyId.Value,
                employeeId.Value,
                cancellationToken))
        {
            return AttendanceContext.Failure(AttendanceError.EmployeeUnavailable);
        }

        return AttendanceContext.Success(
            companyId.Value,
            userId.Value,
            employeeId.Value);
    }

    private static AttendanceRequestValidation ValidateRequest(
        AttendancePunchRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var eventType = AttendanceEventType.ClockIn;

        if (string.IsNullOrWhiteSpace(request.EventType)
            || !Enum.TryParse(
                request.EventType,
                ignoreCase: true,
                out eventType)
            || !Enum.IsDefined(eventType))
        {
            errors[nameof(AttendancePunchRequest.EventType)] =
                ["O tipo de picagem não é válido."];
        }

        if (request.ClientEventId == Guid.Empty)
        {
            errors[nameof(AttendancePunchRequest.ClientEventId)] =
                ["O ClientEventId é obrigatório."];
        }

        return new AttendanceRequestValidation(eventType, errors);
    }

    private static string SerializeEvent(AttendanceEvent attendanceEvent)
    {
        return JsonSerializer.Serialize(new
        {
            attendanceEvent.Id,
            attendanceEvent.CompanyId,
            attendanceEvent.EmployeeId,
            EventType = attendanceEvent.EventType.ToString(),
            attendanceEvent.ServerTimestampUtc,
            attendanceEvent.ClientTimestampUtc,
            attendanceEvent.Latitude,
            attendanceEvent.Longitude,
            attendanceEvent.LocationAccuracyMeters,
            attendanceEvent.WorkSiteId,
            attendanceEvent.ProjectId,
            attendanceEvent.IsInsideGeofence,
            attendanceEvent.DistanceFromWorkSiteMeters,
            Source = attendanceEvent.Source.ToString(),
            attendanceEvent.ClientEventId
        });
    }

    private static AttendancePunchDto Map(
        AttendanceEvent attendanceEvent,
        bool isDuplicate)
    {
        return new AttendancePunchDto(
            attendanceEvent.Id,
            attendanceEvent.EmployeeId,
            attendanceEvent.EventType.ToString(),
            attendanceEvent.ClientEventId,
            attendanceEvent.ServerTimestampUtc,
            attendanceEvent.ClientTimestampUtc,
            attendanceEvent.Latitude,
            attendanceEvent.Longitude,
            attendanceEvent.LocationAccuracyMeters,
            attendanceEvent.WorkSiteId,
            attendanceEvent.ProjectId,
            attendanceEvent.IsInsideGeofence,
            attendanceEvent.DistanceFromWorkSiteMeters,
            isDuplicate);
    }

    private static AttendanceTodayEventDto MapTodayEvent(AttendanceEvent attendanceEvent)
    {
        return new AttendanceTodayEventDto(
            attendanceEvent.Id,
            attendanceEvent.EventType.ToString(),
            attendanceEvent.ServerTimestampUtc,
            attendanceEvent.ClientTimestampUtc,
            attendanceEvent.WorkSiteId,
            attendanceEvent.ProjectId,
            attendanceEvent.IsInsideGeofence,
            attendanceEvent.DistanceFromWorkSiteMeters);
    }

    private static AttendanceStateDto BuildState(
        AttendanceEmployeeStateReference employee,
        AttendanceEventType? lastEventType,
        IReadOnlyList<AttendanceEvent> todayEvents,
        DateTime localDate,
        DateTimeOffset calculatedAtUtc)
    {
        var metrics = CalculateDailyMetrics(todayEvents, calculatedAtUtc);

        return new AttendanceStateDto(
            employee.EmployeeId,
            employee.EmployeeName,
            AttendanceSequenceRules.GetCurrentState(lastEventType),
            AttendanceSequenceRules.GetCurrentStateLabel(lastEventType),
            localDate.ToString("yyyy-MM-dd"),
            lastEventType?.ToString(),
            AttendanceSequenceRules
                .GetAllowedNextEventTypes(lastEventType)
                .Select(eventType => eventType.ToString())
                .ToArray(),
            metrics.ClockInAtUtc,
            metrics.WorkedDurationMinutes,
            metrics.BreakDurationMinutes,
            metrics.BreakCount,
            calculatedAtUtc);
    }

    private static IReadOnlyList<AttendanceBreakDto> BuildBreaks(
        IReadOnlyList<AttendanceEvent> events,
        DateTimeOffset calculatedAtUtc)
    {
        var breaks = new List<AttendanceBreakDto>();
        DateTimeOffset? breakStartedAtUtc = null;

        foreach (var attendanceEvent in events)
        {
            if (attendanceEvent.EventType == AttendanceEventType.BreakStart)
            {
                breakStartedAtUtc = attendanceEvent.ServerTimestampUtc;
                continue;
            }

            if (attendanceEvent.EventType == AttendanceEventType.BreakEnd
                && breakStartedAtUtc.HasValue)
            {
                breaks.Add(new AttendanceBreakDto(
                    breakStartedAtUtc.Value,
                    attendanceEvent.ServerTimestampUtc,
                    ToWholeMinutes(attendanceEvent.ServerTimestampUtc - breakStartedAtUtc.Value)));
                breakStartedAtUtc = null;
            }
        }

        if (breakStartedAtUtc.HasValue)
        {
            breaks.Add(new AttendanceBreakDto(
                breakStartedAtUtc.Value,
                null,
                ToWholeMinutes(calculatedAtUtc - breakStartedAtUtc.Value)));
        }

        return breaks;
    }

    private static DailyAttendanceMetrics CalculateDailyMetrics(
        IReadOnlyList<AttendanceEvent> events,
        DateTimeOffset calculatedAtUtc)
    {
        DateTimeOffset? clockInAtUtc = null;
        DateTimeOffset? workStartedAtUtc = null;
        DateTimeOffset? breakStartedAtUtc = null;
        var workedDuration = TimeSpan.Zero;
        var breakDuration = TimeSpan.Zero;
        var breakCount = 0;

        foreach (var attendanceEvent in events
            .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenBy(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenBy(attendanceEvent => attendanceEvent.Id))
        {
            switch (attendanceEvent.EventType)
            {
                case AttendanceEventType.ClockIn:
                    clockInAtUtc ??= attendanceEvent.ServerTimestampUtc;
                    workStartedAtUtc = attendanceEvent.ServerTimestampUtc;
                    breakStartedAtUtc = null;
                    break;
                case AttendanceEventType.BreakStart:
                    if (workStartedAtUtc.HasValue)
                    {
                        workedDuration += attendanceEvent.ServerTimestampUtc
                            - workStartedAtUtc.Value;
                    }

                    workStartedAtUtc = null;
                    breakStartedAtUtc = attendanceEvent.ServerTimestampUtc;
                    breakCount++;
                    break;
                case AttendanceEventType.BreakEnd:
                    if (breakStartedAtUtc.HasValue)
                    {
                        breakDuration += attendanceEvent.ServerTimestampUtc
                            - breakStartedAtUtc.Value;
                    }

                    breakStartedAtUtc = null;
                    workStartedAtUtc = attendanceEvent.ServerTimestampUtc;
                    break;
                case AttendanceEventType.ClockOut:
                    if (workStartedAtUtc.HasValue)
                    {
                        workedDuration += attendanceEvent.ServerTimestampUtc
                            - workStartedAtUtc.Value;
                    }

                    if (breakStartedAtUtc.HasValue)
                    {
                        breakDuration += attendanceEvent.ServerTimestampUtc
                            - breakStartedAtUtc.Value;
                    }

                    workStartedAtUtc = null;
                    breakStartedAtUtc = null;
                    break;
            }
        }

        if (workStartedAtUtc.HasValue)
        {
            workedDuration += calculatedAtUtc - workStartedAtUtc.Value;
        }

        if (breakStartedAtUtc.HasValue)
        {
            breakDuration += calculatedAtUtc - breakStartedAtUtc.Value;
        }

        return new DailyAttendanceMetrics(
            clockInAtUtc,
            ToWholeMinutes(workedDuration),
            ToWholeMinutes(breakDuration),
            breakCount);
    }

    private static int ToWholeMinutes(TimeSpan duration)
    {
        return Math.Max(0, (int)Math.Floor(duration.TotalMinutes));
    }

    private static TimeZoneInfo GetCompanyTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private sealed record DailyAttendanceMetrics(
        DateTimeOffset? ClockInAtUtc,
        int WorkedDurationMinutes,
        int BreakDurationMinutes,
        int BreakCount);

    private sealed record AttendanceContext(
        Guid CompanyId,
        Guid UserId,
        Guid EmployeeId,
        AttendanceError Error)
    {
        public bool IsSuccess => Error == AttendanceError.None;

        public static AttendanceContext Success(
            Guid companyId,
            Guid userId,
            Guid employeeId)
    {
            return new AttendanceContext(
                companyId,
                userId,
                employeeId,
                AttendanceError.None);
        }

        public static AttendanceContext Failure(AttendanceError error)
        {
            return new AttendanceContext(
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                error);
        }
    }

    private sealed record AttendanceRequestValidation(
        AttendanceEventType EventType,
        IReadOnlyDictionary<string, string[]> Errors);
}
