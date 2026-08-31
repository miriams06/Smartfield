using System.Net;

namespace SmartField.Client.Geolocation;

public enum GeofenceMode
{
    Disabled = 0,
    Warning = 1,
    Block = 2
}

public sealed record GeolocationValidationRequest(
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    Guid? WorkSiteId);

public sealed record GeolocationValidationDto(
    bool IsAccepted,
    bool? IsInsideGeofence,
    decimal? DistanceFromWorkSiteMeters,
    GeofenceMode GeofenceMode,
    string ResultCode,
    string Message);

public sealed record GeolocationApiProblemDetails(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public sealed class GeolocationApiException : Exception
{
    public GeolocationApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
