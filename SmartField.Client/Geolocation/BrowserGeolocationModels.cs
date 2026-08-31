namespace SmartField.Client.Geolocation;

public sealed record BrowserGeolocationResult(
    string Status,
    decimal? Latitude,
    decimal? Longitude,
    decimal? AccuracyMeters,
    string? ErrorMessage)
{
    public bool IsSuccess => Status == BrowserGeolocationStatus.Success;
}

public static class BrowserGeolocationStatus
{
    public const string Success = "success";
    public const string PermissionDenied = "permission-denied";
    public const string PositionUnavailable = "position-unavailable";
    public const string Timeout = "timeout";
    public const string Unsupported = "unsupported";
    public const string UnknownError = "unknown-error";
}
