namespace SmartField.Application.Geolocation;

public interface IGeolocationService
{
    Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
        GeolocationValidationRequest request,
        CancellationToken cancellationToken);
}
