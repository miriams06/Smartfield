using System.Net;

namespace SmartField.Client.Attendance;

public sealed record AttendancePunchRequest(
    string EventType,
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
    IReadOnlyList<AttendanceHistoryEventDto> Events);

public sealed record AttendanceBreakDto(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int Minutes);

public sealed record AttendanceHistoryEventDto(
    Guid Id,
    string EventType,
    DateTimeOffset ServerTimestampUtc,
    DateTimeOffset? ClientTimestampUtc,
    Guid? WorkSiteId,
    Guid? ProjectId,
    bool? IsInsideGeofence,
    decimal? DistanceFromWorkSiteMeters);

public sealed class AttendanceApiException : Exception
{
    public AttendanceApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

internal sealed class AttendanceProblemDetails
{
    public string? Title { get; set; }

    public string? Detail { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}
