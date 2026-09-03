using SmartField.Domain.Enums;

namespace SmartField.Application.Attendance;

public sealed record AttendancePunchRequest(
    string? EventType,
    Guid ClientEventId,
    DateTimeOffset? ClientTimestampUtc,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    Guid? WorkSiteId,
    Guid? ProjectId);

public sealed record AttendancePunchDto(
    Guid Id,
    Guid EmployeeId,
    string EventType,
    Guid ClientEventId,
    DateTimeOffset ServerTimestampUtc,
    DateTimeOffset? ClientTimestampUtc,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    Guid? WorkSiteId,
    Guid? ProjectId,
    bool? IsInsideGeofence,
    decimal? DistanceFromWorkSiteMeters,
    bool IsDuplicate);

public sealed record AttendanceStateDto(
    Guid EmployeeId,
    string EmployeeName,
    string CurrentState,
    string CurrentStateLabel,
    string LocalDate,
    string? LastEventType,
    IReadOnlyList<string> AllowedEventTypes,
    DateTimeOffset? ClockInAtUtc,
    int WorkedDurationMinutes,
    int BreakDurationMinutes,
    int BreakCount,
    DateTimeOffset CalculatedAtUtc);

public sealed record AttendanceTodayDto(
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    IReadOnlyList<AttendanceBreakDto> Breaks,
    int WorkedMinutes,
    int BreakMinutes,
    string CurrentStatus,
    IReadOnlyList<string> NextAllowedActions,
    IReadOnlyList<AttendanceTodayEventDto> Events);

public sealed record AttendanceBreakDto(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int Minutes);

public sealed record AttendanceTodayEventDto(
    Guid Id,
    string EventType,
    DateTimeOffset ServerTimestampUtc,
    DateTimeOffset? ClientTimestampUtc,
    Guid? WorkSiteId,
    Guid? ProjectId,
    bool? IsInsideGeofence,
    decimal? DistanceFromWorkSiteMeters);

public sealed record AttendanceHistoryDayDto(
    string Date,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    int BreakCount,
    int BreakMinutes,
    int WorkedMinutes,
    bool HasOutsideGeofence);

public sealed record AttendanceDayDetailDto(
    string Date,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    IReadOnlyList<AttendanceBreakDto> Breaks,
    int WorkedMinutes,
    int BreakMinutes,
    string CurrentStatus,
    IReadOnlyList<string> NextAllowedActions,
    bool HasOutsideGeofence,
    IReadOnlyList<AttendanceTodayEventDto> Events);

public sealed record AttendanceBackofficeDayFilter(
    DateOnly Date,
    Guid? EmployeeId,
    Guid? WorkSiteId);

public sealed record AttendanceBackofficeExportFilter(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? EmployeeId,
    Guid? WorkSiteId);

public sealed record AttendanceBackofficeDayDto(
    string Date,
    IReadOnlyList<AttendanceBackofficeEmployeeDayDto> Employees);

public sealed record AttendanceBackofficeEmployeeDayDto(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid? DefaultWorkSiteId,
    string? DefaultWorkSiteName,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    int BreakCount,
    int BreakMinutes,
    int WorkedMinutes,
    string CurrentStatus,
    string CurrentStatusLabel,
    bool HasOutsideGeofence);

public sealed record AttendanceBackofficeDayDetailDto(
    string Date,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid? DefaultWorkSiteId,
    string? DefaultWorkSiteName,
    DateTimeOffset? ClockIn,
    DateTimeOffset? ClockOut,
    IReadOnlyList<AttendanceBreakDto> Breaks,
    int WorkedMinutes,
    int BreakMinutes,
    string CurrentStatus,
    string CurrentStatusLabel,
    bool HasOutsideGeofence,
    IReadOnlyList<AttendanceBackofficeEventDto> Events);

public sealed record AttendanceBackofficeEventDto(
    Guid Id,
    string EventType,
    DateTimeOffset ServerTimestampUtc,
    DateTimeOffset? ClientTimestampUtc,
    Guid? WorkSiteId,
    Guid? ProjectId,
    bool? IsInsideGeofence,
    decimal? DistanceFromWorkSiteMeters,
    AttendanceCorrectionDto? Correction);

public sealed record AttendanceBackofficeCsvExportDto(
    string FileName,
    string ContentType,
    string Content);

public sealed record AttendanceCorrectionRequest(
    string? CorrectedEventType,
    DateTimeOffset? CorrectedTimestampUtc,
    string? Reason);

public sealed record AttendanceCorrectionDto(
    Guid Id,
    Guid AttendanceEventId,
    DateTimeOffset OriginalTimestampUtc,
    DateTimeOffset CorrectedTimestampUtc,
    string OriginalEventType,
    string CorrectedEventType,
    string Reason,
    Guid CorrectedByUserId,
    string? CorrectedByUserName,
    DateTimeOffset CreatedAtUtc);

public sealed record AttendanceEmployeeStateReference(
    Guid EmployeeId,
    string EmployeeName,
    string CompanyTimeZone,
    Guid? DefaultWorkSiteId);

public sealed record AttendanceBackofficeEmployeeReference(
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid? DefaultWorkSiteId,
    string? DefaultWorkSiteName);

public sealed record AttendanceReferenceLookup(
    Guid Id,
    string Value);

public sealed record AttendanceEventCorrectionReference(
    Guid Id,
    Guid AttendanceEventId,
    DateTimeOffset OriginalTimestampUtc,
    DateTimeOffset CorrectedTimestampUtc,
    AttendanceEventType OriginalEventType,
    AttendanceEventType CorrectedEventType,
    string Reason,
    Guid CorrectedByUserId,
    string? CorrectedByUserName,
    DateTimeOffset CreatedAtUtc);

public enum AttendanceError
{
    None = 0,
    CompanyUnavailable = 1,
    UserUnavailable = 2,
    EmployeeUnavailable = 3,
    Validation = 4,
    InvalidSequence = 5,
    GeofenceRejected = 6,
    WorkSiteNotFound = 7,
    ProjectNotFound = 8,
    ClientEventConflict = 9,
    EmployeeNotFound = 10,
    AttendanceEventNotFound = 11
}

public sealed record AttendanceResult<T>(
    T? Value,
    AttendanceError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors,
    string? Detail)
    where T : class
{
    public bool IsSuccess => Error == AttendanceError.None;

    public static AttendanceResult<T> Success(T value)
    {
        return new AttendanceResult<T>(
            value,
            AttendanceError.None,
            new Dictionary<string, string[]>(),
            null);
    }

    public static AttendanceResult<T> Failure(
        AttendanceError error,
        string? detail = null)
    {
        return new AttendanceResult<T>(
            null,
            error,
            new Dictionary<string, string[]>(),
            detail);
    }

    public static AttendanceResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new AttendanceResult<T>(
            null,
            AttendanceError.Validation,
            validationErrors,
            null);
    }
}

public sealed class AttendanceClientEventConflictException : Exception
{
    public AttendanceClientEventConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
