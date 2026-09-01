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
