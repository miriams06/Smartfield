using System.Net;
using System.ComponentModel.DataAnnotations;

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

public sealed record GeofenceSettingsDto(
    bool RequireGeolocation,
    GeofenceMode GeofenceMode,
    int DefaultGeofenceRadiusMeters,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    int MaximumLocationAccuracyMeters = 100);

public sealed record UpdateGeofenceSettingsRequest(
    bool RequireGeolocation,
    GeofenceMode GeofenceMode,
    int DefaultGeofenceRadiusMeters,
    int MaximumLocationAccuracyMeters = 100);

public sealed class GeofenceSettingsEditorModel
{
    public bool RequireGeolocation { get; set; }

    public GeofenceMode GeofenceMode { get; set; }

    [Range(1, 10000, ErrorMessage = "O raio por defeito deve estar entre 1 e 10000 metros.")]
    public int DefaultGeofenceRadiusMeters { get; set; } = 100;

    [Range(1, 10000, ErrorMessage = "A precisão máxima aceitável deve estar entre 1 e 10000 metros.")]
    public int MaximumLocationAccuracyMeters { get; set; } = 100;

    public void Load(GeofenceSettingsDto settings)
    {
        RequireGeolocation = settings.RequireGeolocation;
        GeofenceMode = settings.GeofenceMode;
        DefaultGeofenceRadiusMeters = settings.DefaultGeofenceRadiusMeters;
        MaximumLocationAccuracyMeters = settings.MaximumLocationAccuracyMeters;
    }

    public UpdateGeofenceSettingsRequest ToUpdateRequest()
    {
        return new UpdateGeofenceSettingsRequest(
            RequireGeolocation,
            GeofenceMode,
            DefaultGeofenceRadiusMeters,
            MaximumLocationAccuracyMeters);
    }
}

public sealed record GeolocationApiProblemDetails(
    string? Title,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public sealed class GeolocationApiException : Exception
{
    public GeolocationApiException(
        HttpStatusCode statusCode,
        string message,
        string? correlationId = null)
        : base(message)
    {
        StatusCode = statusCode;
        CorrelationId = correlationId;
    }

    public HttpStatusCode StatusCode { get; }

    public string? CorrelationId { get; }
}
