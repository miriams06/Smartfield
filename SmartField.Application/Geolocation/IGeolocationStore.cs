namespace SmartField.Application.Geolocation;

public interface IGeolocationStore
{
    Task<GeofenceValidationReference?> GetValidationReferenceAsync(
        Guid companyId,
        Guid? workSiteId,
        CancellationToken cancellationToken);
}
