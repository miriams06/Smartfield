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

    public async Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.CompanyUnavailable);
        }

        var userId = currentUserProvider.UserId;
        if (!userId.HasValue)
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.UserUnavailable);
        }

        var employeeId = currentUserProvider.EmployeeId;
        if (!employeeId.HasValue
            || !await attendanceStore.EmployeeCanPunchAsync(
                companyId.Value,
                employeeId.Value,
                cancellationToken))
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.EmployeeUnavailable);
        }

        var validation = ValidateRequest(request);
        if (validation.Errors.Count > 0)
        {
            return AttendanceResult<AttendancePunchDto>.Invalid(validation.Errors);
        }

        var existing = await attendanceStore.GetByClientEventIdAsync(
            companyId.Value,
            employeeId.Value,
            request.ClientEventId,
            cancellationToken);

        if (existing is not null)
        {
            return AttendanceResult<AttendancePunchDto>.Success(
                Map(existing, isDuplicate: true));
        }

        if (request.ProjectId.HasValue
            && !await attendanceStore.ProjectExistsAsync(
                companyId.Value,
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
            companyId.Value,
            employeeId.Value,
            cancellationToken);

        if (!IsSequenceAllowed(lastEventType, validation.EventType))
        {
            return AttendanceResult<AttendancePunchDto>.Failure(
                AttendanceError.InvalidSequence,
                BuildSequenceError(lastEventType, validation.EventType));
        }

        var serverTimestampUtc = timeProvider.GetUtcNow();
        var attendanceEvent = new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId.Value,
            EmployeeId = employeeId.Value,
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
            CompanyId = companyId.Value,
            UserId = userId.Value,
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
            CompanyId = companyId.Value,
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
                companyId.Value,
                employeeId.Value,
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

    private static bool IsSequenceAllowed(
        AttendanceEventType? lastEventType,
        AttendanceEventType nextEventType)
    {
        return lastEventType switch
        {
            null => nextEventType == AttendanceEventType.ClockIn,
            AttendanceEventType.ClockIn =>
                nextEventType is AttendanceEventType.BreakStart
                    or AttendanceEventType.ClockOut,
            AttendanceEventType.BreakStart =>
                nextEventType == AttendanceEventType.BreakEnd,
            AttendanceEventType.BreakEnd =>
                nextEventType is AttendanceEventType.BreakStart
                    or AttendanceEventType.ClockOut,
            AttendanceEventType.ClockOut =>
                nextEventType == AttendanceEventType.ClockIn,
            _ => false
        };
    }

    private static string BuildSequenceError(
        AttendanceEventType? lastEventType,
        AttendanceEventType nextEventType)
    {
        var previous = lastEventType?.ToString() ?? "nenhuma picagem";
        return $"A picagem {nextEventType} não é válida depois de {previous}.";
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

    private sealed record AttendanceRequestValidation(
        AttendanceEventType EventType,
        IReadOnlyDictionary<string, string[]> Errors);
}
