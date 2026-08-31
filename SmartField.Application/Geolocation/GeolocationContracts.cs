using SmartField.Domain.Enums;

namespace SmartField.Application.Geolocation;

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

public sealed record GeofenceValidationReference(
    GeofenceMode GeofenceMode,
    int DefaultGeofenceRadiusMeters,
    WorkSiteGeofenceReference? WorkSite);

public sealed record WorkSiteGeofenceReference(
    Guid Id,
    decimal? Latitude,
    decimal? Longitude,
    int? GeofenceRadiusMeters);

public enum GeolocationError
{
    None = 0,
    CompanyUnavailable = 1,
    Validation = 2,
    WorkSiteNotFound = 3
}

public sealed record GeolocationResult<T>(
    T? Value,
    GeolocationError Error,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
    where T : class
{
    public bool IsSuccess => Error == GeolocationError.None;

    public static GeolocationResult<T> Success(T value)
    {
        return new GeolocationResult<T>(
            value,
            GeolocationError.None,
            new Dictionary<string, string[]>());
    }

    public static GeolocationResult<T> Failure(GeolocationError error)
    {
        return new GeolocationResult<T>(
            null,
            error,
            new Dictionary<string, string[]>());
    }

    public static GeolocationResult<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new GeolocationResult<T>(
            null,
            GeolocationError.Validation,
            validationErrors);
    }
}
