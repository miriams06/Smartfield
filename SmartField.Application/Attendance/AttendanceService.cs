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
        var clockOut = GetLastClockOut(todayEvents);

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

    public async Task<AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>> GetHistoryAsync(
        CancellationToken cancellationToken)
    {
        var historyContext = await GetHistoryContextAsync(cancellationToken);
        if (!historyContext.IsSuccess)
        {
            return AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>.Failure(
                historyContext.Error);
        }

        var events = await attendanceStore.GetEventsFromAsync(
            historyContext.CompanyId,
            historyContext.EmployeeId,
            DateTimeOffset.MinValue,
            cancellationToken);

        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyNow = TimeZoneInfo.ConvertTime(
            calculatedAtUtc,
            historyContext.CompanyTimeZone);
        var companyToday = DateOnly.FromDateTime(companyNow.Date);

        var days = events
            .GroupBy(attendanceEvent =>
                DateOnly.FromDateTime(
                    TimeZoneInfo.ConvertTime(
                        attendanceEvent.ServerTimestampUtc,
                        historyContext.CompanyTimeZone).Date))
            .OrderByDescending(group => group.Key)
            .Select(group =>
            {
                var dayEvents = OrderEvents(group).ToArray();
                var calculationPoint = GetHistoryCalculationPoint(
                    group.Key,
                    companyToday,
                    dayEvents,
                    calculatedAtUtc);
                var metrics = CalculateDailyMetrics(dayEvents, calculationPoint);

                return new AttendanceHistoryDayDto(
                    group.Key.ToString("yyyy-MM-dd"),
                    metrics.ClockInAtUtc,
                    GetLastClockOut(dayEvents),
                    metrics.BreakCount,
                    metrics.BreakDurationMinutes,
                    metrics.WorkedDurationMinutes,
                    HasOutsideGeofence(dayEvents));
            })
            .ToArray();

        return AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>.Success(days);
    }

    public async Task<AttendanceResult<AttendanceDayDetailDto>> GetDayAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var historyContext = await GetHistoryContextAsync(cancellationToken);
        if (!historyContext.IsSuccess)
        {
            return AttendanceResult<AttendanceDayDetailDto>.Failure(
                historyContext.Error);
        }

        var dayStartUtc = ConvertLocalDateToUtc(date, historyContext.CompanyTimeZone);
        var nextDayStartUtc = ConvertLocalDateToUtc(
            date.AddDays(1),
            historyContext.CompanyTimeZone);
        var loadedEvents = await attendanceStore.GetEventsFromAsync(
            historyContext.CompanyId,
            historyContext.EmployeeId,
            dayStartUtc,
            cancellationToken);
        var dayEvents = loadedEvents
            .Where(attendanceEvent => attendanceEvent.ServerTimestampUtc < nextDayStartUtc)
            .Pipe(OrderEvents)
            .ToArray();

        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyNow = TimeZoneInfo.ConvertTime(
            calculatedAtUtc,
            historyContext.CompanyTimeZone);
        var companyToday = DateOnly.FromDateTime(companyNow.Date);
        var calculationPoint = GetHistoryCalculationPoint(
            date,
            companyToday,
            dayEvents,
            calculatedAtUtc);
        var metrics = CalculateDailyMetrics(dayEvents, calculationPoint);
        var lastEventType = dayEvents.LastOrDefault()?.EventType;

        return AttendanceResult<AttendanceDayDetailDto>.Success(
            new AttendanceDayDetailDto(
                date.ToString("yyyy-MM-dd"),
                metrics.ClockInAtUtc,
                GetLastClockOut(dayEvents),
                BuildBreaks(dayEvents, calculationPoint),
                metrics.WorkedDurationMinutes,
                metrics.BreakDurationMinutes,
                AttendanceSequenceRules.GetCurrentState(lastEventType),
                AttendanceSequenceRules
                    .GetAllowedNextEventTypes(lastEventType)
                    .Select(eventType => eventType.ToString())
                    .ToArray(),
                HasOutsideGeofence(dayEvents),
                dayEvents.Select(MapTodayEvent).ToArray()));
    }

    public async Task<AttendanceResult<AttendanceBackofficeDayDto>> GetBackofficeDayAsync(
        AttendanceBackofficeDayFilter filter,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateBackofficeFilter(filter);
        if (validationErrors.Count > 0)
        {
            return AttendanceResult<AttendanceBackofficeDayDto>.Invalid(validationErrors);
        }

        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return AttendanceResult<AttendanceBackofficeDayDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var companyTimeZone = await GetCurrentCompanyTimeZoneAsync(
            companyId.Value,
            cancellationToken);
        if (companyTimeZone is null)
        {
            return AttendanceResult<AttendanceBackofficeDayDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var employees = await attendanceStore.GetBackofficeEmployeesAsync(
            companyId.Value,
            filter.EmployeeId,
            cancellationToken);
        var dayEvents = await GetCompanyDayEventsAsync(
            companyId.Value,
            filter.Date,
            companyTimeZone,
            filter.EmployeeId,
            cancellationToken);
        var latestCorrections = await GetLatestCorrectionsByEventIdAsync(
            companyId.Value,
            dayEvents,
            cancellationToken);
        var eventsByEmployee = dayEvents
            .GroupBy(attendanceEvent => attendanceEvent.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => OrderEvents(
                    group.Select(attendanceEvent =>
                        ApplyCorrection(attendanceEvent, latestCorrections)))
                    .ToArray());
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(calculatedAtUtc, companyTimeZone).Date);

        var rows = employees
            .Where(employee => ShouldIncludeBackofficeEmployee(
                employee,
                eventsByEmployee,
                filter.WorkSiteId))
            .OrderBy(employee => employee.EmployeeName)
            .ThenBy(employee => employee.EmployeeNumber)
            .Select(employee =>
            {
                eventsByEmployee.TryGetValue(employee.EmployeeId, out var employeeEvents);
                return BuildBackofficeRow(
                    employee,
                    employeeEvents ?? [],
                    filter.Date,
                    companyToday,
                    calculatedAtUtc);
            })
            .ToArray();

        return AttendanceResult<AttendanceBackofficeDayDto>.Success(
            new AttendanceBackofficeDayDto(
                filter.Date.ToString("yyyy-MM-dd"),
                rows));
    }

    public async Task<AttendanceResult<AttendanceBackofficeDayDetailDto>> GetBackofficeDayDetailAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (employeeId == Guid.Empty)
        {
            return AttendanceResult<AttendanceBackofficeDayDetailDto>.Invalid(
                new Dictionary<string, string[]>
                {
                    [nameof(employeeId)] = ["O funcionário é obrigatório."]
                });
        }

        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return AttendanceResult<AttendanceBackofficeDayDetailDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var companyTimeZone = await GetCurrentCompanyTimeZoneAsync(
            companyId.Value,
            cancellationToken);
        if (companyTimeZone is null)
        {
            return AttendanceResult<AttendanceBackofficeDayDetailDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var employees = await attendanceStore.GetBackofficeEmployeesAsync(
            companyId.Value,
            employeeId,
            cancellationToken);
        var employee = employees.SingleOrDefault();
        if (employee is null)
        {
            return AttendanceResult<AttendanceBackofficeDayDetailDto>.Failure(
                AttendanceError.EmployeeNotFound);
        }

        var dayEvents = await GetCompanyDayEventsAsync(
            companyId.Value,
            date,
            companyTimeZone,
            employeeId,
            cancellationToken);
        var latestCorrections = await GetLatestCorrectionsByEventIdAsync(
            companyId.Value,
            dayEvents,
            cancellationToken);
        var orderedEvents = OrderEvents(
                dayEvents.Select(attendanceEvent =>
                    ApplyCorrection(attendanceEvent, latestCorrections)))
            .ToArray();
        var originalEvents = OrderEvents(dayEvents).ToArray();
        var calculatedAtUtc = timeProvider.GetUtcNow();
        var companyToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(calculatedAtUtc, companyTimeZone).Date);
        var calculationPoint = GetHistoryCalculationPoint(
            date,
            companyToday,
            orderedEvents,
            calculatedAtUtc);
        var metrics = CalculateDailyMetrics(orderedEvents, calculationPoint);
        var lastEventType = orderedEvents.LastOrDefault()?.EventType;

        return AttendanceResult<AttendanceBackofficeDayDetailDto>.Success(
            new AttendanceBackofficeDayDetailDto(
                date.ToString("yyyy-MM-dd"),
                employee.EmployeeId,
                employee.EmployeeNumber,
                employee.EmployeeName,
                employee.DefaultWorkSiteId,
                employee.DefaultWorkSiteName,
                metrics.ClockInAtUtc,
                GetLastClockOut(orderedEvents),
                BuildBreaks(orderedEvents, calculationPoint),
                metrics.WorkedDurationMinutes,
                metrics.BreakDurationMinutes,
                AttendanceSequenceRules.GetCurrentState(lastEventType),
                AttendanceSequenceRules.GetCurrentStateLabel(lastEventType),
                HasOutsideGeofence(originalEvents),
                originalEvents
                    .Select(attendanceEvent =>
                        MapBackofficeEvent(attendanceEvent, latestCorrections))
                    .ToArray()));
    }

    public async Task<AttendanceResult<AttendanceCorrectionDto>> CorrectBackofficeEventAsync(
        Guid attendanceEventId,
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCorrectionRequest(request);
        if (validation.Errors.Count > 0)
        {
            return AttendanceResult<AttendanceCorrectionDto>.Invalid(validation.Errors);
        }

        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return AttendanceResult<AttendanceCorrectionDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var userId = currentUserProvider.UserId;
        if (!userId.HasValue)
        {
            return AttendanceResult<AttendanceCorrectionDto>.Failure(
                AttendanceError.UserUnavailable);
        }

        var originalEvent = await attendanceStore.GetEventAsync(
            companyId.Value,
            attendanceEventId,
            cancellationToken);
        if (originalEvent is null)
        {
            return AttendanceResult<AttendanceCorrectionDto>.Failure(
                AttendanceError.AttendanceEventNotFound);
        }

        var nowUtc = timeProvider.GetUtcNow();
        var correction = new AttendanceCorrection
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            AttendanceEventId = originalEvent.Id,
            OriginalTimestampUtc = originalEvent.ServerTimestampUtc,
            CorrectedTimestampUtc = validation.CorrectedTimestampUtc,
            OriginalEventType = originalEvent.EventType,
            CorrectedEventType = validation.CorrectedEventType,
            Reason = validation.Reason,
            CorrectedByUserId = userId.Value,
            CreatedAtUtc = nowUtc
        };
        var payload = SerializeCorrection(correction);

        attendanceStore.Add(correction);
        attendanceStore.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            UserId = userId.Value,
            EntityType = nameof(AttendanceCorrection),
            EntityId = correction.Id,
            Action = "Created",
            OldValues = SerializeEvent(originalEvent),
            NewValues = payload,
            TimestampUtc = nowUtc,
            CreatedAtUtc = nowUtc
        });

        await attendanceStore.SaveChangesAsync(cancellationToken);

        return AttendanceResult<AttendanceCorrectionDto>.Success(
            MapCorrection(
                new AttendanceEventCorrectionReference(
                    correction.Id,
                    correction.AttendanceEventId,
                    correction.OriginalTimestampUtc,
                    correction.CorrectedTimestampUtc,
                    correction.OriginalEventType,
                    correction.CorrectedEventType,
                    correction.Reason,
                    correction.CorrectedByUserId,
                    null,
                    correction.CreatedAtUtc)));
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

    private async Task<AttendanceHistoryContext> GetHistoryContextAsync(
        CancellationToken cancellationToken)
    {
        var context = await GetAttendanceContextAsync(cancellationToken);
        if (!context.IsSuccess)
        {
            return AttendanceHistoryContext.Failure(context.Error);
        }

        var employee = await attendanceStore.GetEmployeeStateReferenceAsync(
            context.CompanyId,
            context.EmployeeId,
            cancellationToken);

        if (employee is null)
        {
            return AttendanceHistoryContext.Failure(
                AttendanceError.EmployeeUnavailable);
        }

        return AttendanceHistoryContext.Success(
            context.CompanyId,
            context.EmployeeId,
            GetCompanyTimeZone(employee.CompanyTimeZone));
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

    private static AttendanceCorrectionValidation ValidateCorrectionRequest(
        AttendanceCorrectionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var eventType = AttendanceEventType.ClockIn;

        if (string.IsNullOrWhiteSpace(request.CorrectedEventType)
            || !Enum.TryParse(
                request.CorrectedEventType,
                ignoreCase: true,
                out eventType)
            || !Enum.IsDefined(eventType))
        {
            errors[nameof(request.CorrectedEventType)] =
                ["O tipo corrigido não é válido."];
        }

        if (!request.CorrectedTimestampUtc.HasValue)
        {
            errors[nameof(request.CorrectedTimestampUtc)] =
                ["A nova hora é obrigatória."];
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            errors[nameof(request.Reason)] =
                ["O motivo da correção é obrigatório."];
        }
        else if (reason.Length > 1000)
        {
            errors[nameof(request.Reason)] =
                ["O motivo não pode exceder 1000 caracteres."];
        }

        return new AttendanceCorrectionValidation(
            eventType,
            request.CorrectedTimestampUtc?.ToUniversalTime() ?? DateTimeOffset.MinValue,
            reason ?? string.Empty,
            errors);
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

    private static string SerializeCorrection(AttendanceCorrection correction)
    {
        return JsonSerializer.Serialize(new
        {
            correction.Id,
            correction.CompanyId,
            correction.AttendanceEventId,
            correction.OriginalTimestampUtc,
            correction.CorrectedTimestampUtc,
            OriginalEventType = correction.OriginalEventType.ToString(),
            CorrectedEventType = correction.CorrectedEventType.ToString(),
            correction.Reason,
            correction.CorrectedByUserId,
            correction.CreatedAtUtc
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

    private static AttendanceBackofficeEventDto MapBackofficeEvent(
        AttendanceEvent attendanceEvent,
        IReadOnlyDictionary<Guid, AttendanceEventCorrectionReference> correctionsByEventId)
    {
        correctionsByEventId.TryGetValue(attendanceEvent.Id, out var correction);

        return new AttendanceBackofficeEventDto(
            attendanceEvent.Id,
            attendanceEvent.EventType.ToString(),
            attendanceEvent.ServerTimestampUtc,
            attendanceEvent.ClientTimestampUtc,
            attendanceEvent.WorkSiteId,
            attendanceEvent.ProjectId,
            attendanceEvent.IsInsideGeofence,
            attendanceEvent.DistanceFromWorkSiteMeters,
            correction is null ? null : MapCorrection(correction));
    }

    private static AttendanceCorrectionDto MapCorrection(
        AttendanceEventCorrectionReference correction)
    {
        return new AttendanceCorrectionDto(
            correction.Id,
            correction.AttendanceEventId,
            correction.OriginalTimestampUtc,
            correction.CorrectedTimestampUtc,
            correction.OriginalEventType.ToString(),
            correction.CorrectedEventType.ToString(),
            correction.Reason,
            correction.CorrectedByUserId,
            correction.CorrectedByUserName,
            correction.CreatedAtUtc);
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

        foreach (var attendanceEvent in OrderEvents(events))
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

    private async Task<TimeZoneInfo?> GetCurrentCompanyTimeZoneAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await attendanceStore.GetCompanyTimeZoneAsync(
            companyId,
            cancellationToken);

        return timeZoneId is null
            ? null
            : GetCompanyTimeZone(timeZoneId);
    }

    private async Task<IReadOnlyList<AttendanceEvent>> GetCompanyDayEventsAsync(
        Guid companyId,
        DateOnly date,
        TimeZoneInfo companyTimeZone,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var dayStartUtc = ConvertLocalDateToUtc(date, companyTimeZone);
        var nextDayStartUtc = ConvertLocalDateToUtc(date.AddDays(1), companyTimeZone);

        return await attendanceStore.GetEventsBetweenAsync(
            companyId,
            dayStartUtc,
            nextDayStartUtc,
            employeeId,
            cancellationToken);
    }

    private static AttendanceBackofficeEmployeeDayDto BuildBackofficeRow(
        AttendanceBackofficeEmployeeReference employee,
        IReadOnlyList<AttendanceEvent> orderedEvents,
        DateOnly date,
        DateOnly companyToday,
        DateTimeOffset calculatedAtUtc)
    {
        var calculationPoint = GetHistoryCalculationPoint(
            date,
            companyToday,
            orderedEvents,
            calculatedAtUtc);
        var metrics = CalculateDailyMetrics(orderedEvents, calculationPoint);
        var lastEventType = orderedEvents.LastOrDefault()?.EventType;

        return new AttendanceBackofficeEmployeeDayDto(
            employee.EmployeeId,
            employee.EmployeeNumber,
            employee.EmployeeName,
            employee.DefaultWorkSiteId,
            employee.DefaultWorkSiteName,
            metrics.ClockInAtUtc,
            GetLastClockOut(orderedEvents),
            metrics.BreakCount,
            metrics.BreakDurationMinutes,
            metrics.WorkedDurationMinutes,
            AttendanceSequenceRules.GetCurrentState(lastEventType),
            AttendanceSequenceRules.GetCurrentStateLabel(lastEventType),
            HasOutsideGeofence(orderedEvents));
    }

    private static bool ShouldIncludeBackofficeEmployee(
        AttendanceBackofficeEmployeeReference employee,
        IReadOnlyDictionary<Guid, AttendanceEvent[]> eventsByEmployee,
        Guid? workSiteId)
    {
        if (!workSiteId.HasValue)
        {
            return true;
        }

        if (employee.DefaultWorkSiteId == workSiteId.Value)
        {
            return true;
        }

        return eventsByEmployee.TryGetValue(employee.EmployeeId, out var events)
            && events.Any(attendanceEvent => attendanceEvent.WorkSiteId == workSiteId.Value);
    }

    private static IReadOnlyDictionary<string, string[]> ValidateBackofficeFilter(
        AttendanceBackofficeDayFilter filter)
    {
        var errors = new Dictionary<string, string[]>();

        if (filter.EmployeeId == Guid.Empty)
        {
            errors[nameof(filter.EmployeeId)] = ["O funcionário selecionado não é válido."];
        }

        if (filter.WorkSiteId == Guid.Empty)
        {
            errors[nameof(filter.WorkSiteId)] = ["O local de trabalho selecionado não é válido."];
        }

        return errors;
    }

    private async Task<IReadOnlyDictionary<Guid, AttendanceEventCorrectionReference>> GetLatestCorrectionsByEventIdAsync(
        Guid companyId,
        IReadOnlyList<AttendanceEvent> events,
        CancellationToken cancellationToken)
    {
        var eventIds = events
            .Select(attendanceEvent => attendanceEvent.Id)
            .Distinct()
            .ToArray();
        if (eventIds.Length == 0)
        {
            return new Dictionary<Guid, AttendanceEventCorrectionReference>();
        }

        var corrections = await attendanceStore.GetCorrectionsForEventsAsync(
            companyId,
            eventIds,
            cancellationToken);

        return corrections
            .GroupBy(correction => correction.AttendanceEventId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(correction => correction.CreatedAtUtc)
                    .ThenByDescending(correction => correction.Id)
                    .First());
    }

    private static AttendanceEvent ApplyCorrection(
        AttendanceEvent attendanceEvent,
        IReadOnlyDictionary<Guid, AttendanceEventCorrectionReference> correctionsByEventId)
    {
        if (!correctionsByEventId.TryGetValue(attendanceEvent.Id, out var correction))
        {
            return attendanceEvent;
        }

        return new AttendanceEvent
        {
            Id = attendanceEvent.Id,
            CompanyId = attendanceEvent.CompanyId,
            EmployeeId = attendanceEvent.EmployeeId,
            EventType = correction.CorrectedEventType,
            ServerTimestampUtc = correction.CorrectedTimestampUtc,
            ClientTimestampUtc = attendanceEvent.ClientTimestampUtc,
            Latitude = attendanceEvent.Latitude,
            Longitude = attendanceEvent.Longitude,
            LocationAccuracyMeters = attendanceEvent.LocationAccuracyMeters,
            WorkSiteId = attendanceEvent.WorkSiteId,
            ProjectId = attendanceEvent.ProjectId,
            IsInsideGeofence = attendanceEvent.IsInsideGeofence,
            DistanceFromWorkSiteMeters = attendanceEvent.DistanceFromWorkSiteMeters,
            Source = attendanceEvent.Source,
            ClientEventId = attendanceEvent.ClientEventId,
            Notes = attendanceEvent.Notes,
            CreatedAtUtc = attendanceEvent.CreatedAtUtc
        };
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

        foreach (var attendanceEvent in OrderEvents(events))
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

    private static IEnumerable<AttendanceEvent> OrderEvents(
        IEnumerable<AttendanceEvent> events)
    {
        return events
            .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenBy(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenBy(attendanceEvent => attendanceEvent.Id);
    }

    private static DateTimeOffset? GetLastClockOut(
        IEnumerable<AttendanceEvent> events)
    {
        return events
            .Where(attendanceEvent => attendanceEvent.EventType == AttendanceEventType.ClockOut)
            .Select(attendanceEvent => (DateTimeOffset?)attendanceEvent.ServerTimestampUtc)
            .LastOrDefault();
    }

    private static bool HasOutsideGeofence(IEnumerable<AttendanceEvent> events)
    {
        return events.Any(attendanceEvent => attendanceEvent.IsInsideGeofence == false);
    }

    private static DateTimeOffset GetHistoryCalculationPoint(
        DateOnly date,
        DateOnly companyToday,
        IReadOnlyList<AttendanceEvent> events,
        DateTimeOffset calculatedAtUtc)
    {
        if (date == companyToday || events.Count == 0)
        {
            return calculatedAtUtc;
        }

        return events[^1].ServerTimestampUtc;
    }

    private static DateTimeOffset ConvertLocalDateToUtc(
        DateOnly date,
        TimeZoneInfo timeZone)
    {
        var localDateTime = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
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

    private sealed record AttendanceHistoryContext(
        Guid CompanyId,
        Guid EmployeeId,
        TimeZoneInfo CompanyTimeZone,
        AttendanceError Error)
    {
        public bool IsSuccess => Error == AttendanceError.None;

        public static AttendanceHistoryContext Success(
            Guid companyId,
            Guid employeeId,
            TimeZoneInfo companyTimeZone)
        {
            return new AttendanceHistoryContext(
                companyId,
                employeeId,
                companyTimeZone,
                AttendanceError.None);
        }

        public static AttendanceHistoryContext Failure(AttendanceError error)
        {
            return new AttendanceHistoryContext(
                Guid.Empty,
                Guid.Empty,
                TimeZoneInfo.Utc,
                error);
        }
    }

    private sealed record AttendanceRequestValidation(
        AttendanceEventType EventType,
        IReadOnlyDictionary<string, string[]> Errors);

    private sealed record AttendanceCorrectionValidation(
        AttendanceEventType CorrectedEventType,
        DateTimeOffset CorrectedTimestampUtc,
        string Reason,
        IReadOnlyDictionary<string, string[]> Errors);
}

internal static class EnumerablePipeExtensions
{
    public static TResult Pipe<TSource, TResult>(
        this TSource source,
        Func<TSource, TResult> selector)
    {
        return selector(source);
    }
}
