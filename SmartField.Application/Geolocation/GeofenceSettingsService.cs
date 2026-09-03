using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Geolocation;

public sealed class GeofenceSettingsService : IGeofenceSettingsService
{
    private const int MinimumRadiusMeters = 1;
    private const int MaximumRadiusMeters = 10000;

    private readonly IGeofenceSettingsStore geofenceSettingsStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly TimeProvider timeProvider;

    public GeofenceSettingsService(
        IGeofenceSettingsStore geofenceSettingsStore,
        ICurrentCompanyProvider currentCompanyProvider,
        TimeProvider timeProvider)
    {
        this.geofenceSettingsStore = geofenceSettingsStore;
        this.currentCompanyProvider = currentCompanyProvider;
        this.timeProvider = timeProvider;
    }

    public async Task<GeolocationResult<GeofenceSettingsDto>> GetAsync(
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return GeolocationResult<GeofenceSettingsDto>.Failure(
                GeolocationError.CompanyUnavailable);
        }

        var settings = await GetOrCreateSettingsAsync(
            companyId.Value,
            cancellationToken);

        return GeolocationResult<GeofenceSettingsDto>.Success(Map(settings));
    }

    public async Task<GeolocationResult<GeofenceSettingsDto>> UpdateAsync(
        UpdateGeofenceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return GeolocationResult<GeofenceSettingsDto>.Failure(
                GeolocationError.CompanyUnavailable);
        }

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return GeolocationResult<GeofenceSettingsDto>.Invalid(validationErrors);
        }

        var settings = await GetOrCreateSettingsAsync(
            companyId.Value,
            cancellationToken);

        settings.RequireGeolocation = request.RequireGeolocation;
        settings.GeofenceMode = request.GeofenceMode;
        settings.DefaultGeofenceRadiusMeters = request.DefaultGeofenceRadiusMeters;
        settings.UpdatedAtUtc = timeProvider.GetUtcNow();

        await geofenceSettingsStore.SaveChangesAsync(cancellationToken);

        return GeolocationResult<GeofenceSettingsDto>.Success(Map(settings));
    }

    private async Task<CompanySettings> GetOrCreateSettingsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var settings = await geofenceSettingsStore.FindAsync(
            companyId,
            cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        var now = timeProvider.GetUtcNow();
        settings = new CompanySettings
        {
            CompanyId = companyId,
            RequireGeolocation = false,
            GeofenceMode = GeofenceMode.Disabled,
            AllowBreaks = true,
            AllowProjectSelection = false,
            RequireProjectSelection = false,
            DefaultGeofenceRadiusMeters = 100,
            CreatedAtUtc = now
        };

        geofenceSettingsStore.Add(settings);
        await geofenceSettingsStore.SaveChangesAsync(cancellationToken);

        return settings;
    }

    private static Dictionary<string, string[]> Validate(
        UpdateGeofenceSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Enum.IsDefined(request.GeofenceMode))
        {
            errors[nameof(request.GeofenceMode)] =
                ["O modo de geofence selecionado não é válido."];
        }

        if (request.DefaultGeofenceRadiusMeters is < MinimumRadiusMeters or > MaximumRadiusMeters)
        {
            errors[nameof(request.DefaultGeofenceRadiusMeters)] =
                [$"O raio por defeito deve estar entre {MinimumRadiusMeters} e {MaximumRadiusMeters} metros."];
        }

        return errors;
    }

    private static GeofenceSettingsDto Map(CompanySettings settings)
    {
        return new GeofenceSettingsDto(
            settings.RequireGeolocation,
            settings.GeofenceMode,
            settings.DefaultGeofenceRadiusMeters,
            settings.CreatedAtUtc,
            settings.UpdatedAtUtc);
    }
}
