using SmartField.Application.Abstractions;
using SmartField.Domain.Enums;

namespace SmartField.Application.Geolocation;

public sealed class GeolocationService : IGeolocationService
{
    private const string LatitudePropertyName = nameof(GeolocationValidationRequest.Latitude);
    private const string LongitudePropertyName = nameof(GeolocationValidationRequest.Longitude);
    private const string AccuracyMetersPropertyName = nameof(GeolocationValidationRequest.AccuracyMeters);

    private readonly IGeolocationStore geolocationStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;

    public GeolocationService(
        IGeolocationStore geolocationStore,
        ICurrentCompanyProvider currentCompanyProvider)
    {
        this.geolocationStore = geolocationStore;
        this.currentCompanyProvider = currentCompanyProvider;
    }

    public async Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
        GeolocationValidationRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return GeolocationResult<GeolocationValidationDto>.Failure(
                GeolocationError.CompanyUnavailable);
        }

        var validationErrors = ValidateInput(request);
        if (validationErrors.Count > 0)
        {
            return GeolocationResult<GeolocationValidationDto>.Invalid(validationErrors);
        }

        var reference = await geolocationStore.GetValidationReferenceAsync(
            companyId.Value,
            request.WorkSiteId,
            cancellationToken);

        if (reference is null)
        {
            return GeolocationResult<GeolocationValidationDto>.Failure(
                GeolocationError.WorkSiteNotFound);
        }

        var response = ValidateAgainstReference(request, reference);

        return GeolocationResult<GeolocationValidationDto>.Success(response);
    }

    private static Dictionary<string, string[]> ValidateInput(
        GeolocationValidationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            errors[LatitudePropertyName] =
                ["Latitude e longitude devem ser enviadas em conjunto."];
            errors[LongitudePropertyName] =
                ["Latitude e longitude devem ser enviadas em conjunto."];
        }

        if (request.Latitude is < -90 or > 90)
        {
            errors[LatitudePropertyName] =
                ["A latitude deve estar entre -90 e 90."];
        }

        if (request.Longitude is < -180 or > 180)
        {
            errors[LongitudePropertyName] =
                ["A longitude deve estar entre -180 e 180."];
        }

        if (request.AccuracyMeters is < 0)
        {
            errors[AccuracyMetersPropertyName] =
                ["A precisão da localização não pode ser negativa."];
        }

        return errors;
    }

    private static GeolocationValidationDto ValidateAgainstReference(
        GeolocationValidationRequest request,
        GeofenceValidationReference reference)
    {
        if (reference.GeofenceMode == GeofenceMode.Disabled)
        {
            return new GeolocationValidationDto(
                true,
                null,
                TryCalculateDistance(request, reference.WorkSite),
                reference.GeofenceMode,
                "GeofenceDisabled",
                "A validação de geofence está desativada.");
        }

        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
        {
            return BuildUnavailableLocationResult(reference.GeofenceMode);
        }

        if (reference.WorkSite?.Latitude is null || reference.WorkSite.Longitude is null)
        {
            return BuildUnavailableWorkSiteResult(reference.GeofenceMode);
        }

        var radiusMeters = reference.WorkSite.GeofenceRadiusMeters
            ?? reference.DefaultGeofenceRadiusMeters;

        if (radiusMeters <= 0)
        {
            return BuildUnavailableWorkSiteResult(reference.GeofenceMode);
        }

        var distanceMeters = CalculateDistanceMeters(
            request.Latitude.Value,
            request.Longitude.Value,
            reference.WorkSite.Latitude.Value,
            reference.WorkSite.Longitude.Value);
        var isInsideGeofence = distanceMeters <= radiusMeters;

        if (isInsideGeofence)
        {
            return new GeolocationValidationDto(
                true,
                true,
                distanceMeters,
                reference.GeofenceMode,
                "InsideGeofence",
                "A localização está dentro do raio permitido.");
        }

        return reference.GeofenceMode == GeofenceMode.Warning
            ? new GeolocationValidationDto(
                true,
                false,
                distanceMeters,
                reference.GeofenceMode,
                "OutsideGeofenceWarning",
                "A localização está fora do raio permitido, mas a picagem pode prosseguir.")
            : new GeolocationValidationDto(
                false,
                false,
                distanceMeters,
                reference.GeofenceMode,
                "OutsideGeofenceBlocked",
                "A localização está fora do raio permitido.");
    }

    private static decimal? TryCalculateDistance(
        GeolocationValidationRequest request,
        WorkSiteGeofenceReference? workSite)
    {
        if (!request.Latitude.HasValue
            || !request.Longitude.HasValue
            || workSite?.Latitude is null
            || workSite.Longitude is null)
        {
            return null;
        }

        return CalculateDistanceMeters(
            request.Latitude.Value,
            request.Longitude.Value,
            workSite.Latitude.Value,
            workSite.Longitude.Value);
    }

    private static GeolocationValidationDto BuildUnavailableLocationResult(
        GeofenceMode geofenceMode)
    {
        return new GeolocationValidationDto(
            geofenceMode == GeofenceMode.Warning,
            false,
            null,
            geofenceMode,
            geofenceMode == GeofenceMode.Warning
                ? "LocationUnavailableWarning"
                : "LocationUnavailableBlocked",
            geofenceMode == GeofenceMode.Warning
                ? "A localização não está disponível, mas a picagem pode prosseguir com aviso."
                : "A localização é obrigatória para este modo de geofence.");
    }

    private static GeolocationValidationDto BuildUnavailableWorkSiteResult(
        GeofenceMode geofenceMode)
    {
        return new GeolocationValidationDto(
            geofenceMode == GeofenceMode.Warning,
            false,
            null,
            geofenceMode,
            geofenceMode == GeofenceMode.Warning
                ? "WorkSiteLocationUnavailableWarning"
                : "WorkSiteLocationUnavailableBlocked",
            geofenceMode == GeofenceMode.Warning
                ? "O local de trabalho não tem geofence configurada, mas a picagem pode prosseguir com aviso."
                : "O local de trabalho não tem geofence configurada para validar a localização.");
    }

    internal static decimal CalculateDistanceMeters(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude)
    {
        const double EarthRadiusMeters = 6371000;

        var originLatitudeRadians = DegreesToRadians((double)originLatitude);
        var destinationLatitudeRadians = DegreesToRadians((double)destinationLatitude);
        var latitudeDelta = DegreesToRadians((double)(destinationLatitude - originLatitude));
        var longitudeDelta = DegreesToRadians((double)(destinationLongitude - originLongitude));

        var haversine =
            Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(originLatitudeRadians)
            * Math.Cos(destinationLatitudeRadians)
            * Math.Sin(longitudeDelta / 2)
            * Math.Sin(longitudeDelta / 2);

        var angularDistance = 2 * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1 - haversine));

        return decimal.Round((decimal)(EarthRadiusMeters * angularDistance), 2);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
