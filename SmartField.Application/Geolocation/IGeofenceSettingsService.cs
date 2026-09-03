namespace SmartField.Application.Geolocation;

public interface IGeofenceSettingsService
{
    Task<GeolocationResult<GeofenceSettingsDto>> GetAsync(
        CancellationToken cancellationToken);

    Task<GeolocationResult<GeofenceSettingsDto>> UpdateAsync(
        UpdateGeofenceSettingsRequest request,
        CancellationToken cancellationToken);
}
