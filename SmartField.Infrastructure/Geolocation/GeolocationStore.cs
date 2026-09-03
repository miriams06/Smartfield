using Microsoft.EntityFrameworkCore;
using SmartField.Application.Geolocation;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Geolocation;

public sealed class GeolocationStore : IGeolocationStore
{
    private readonly SmartFieldDbContext dbContext;

    public GeolocationStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<GeofenceValidationReference?> GetValidationReferenceAsync(
        Guid companyId,
        Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.CompanySettings
            .AsNoTracking()
            .Where(companySettings => companySettings.CompanyId == companyId)
            .Select(companySettings => new
            {
                companySettings.RequireGeolocation,
                companySettings.GeofenceMode,
                companySettings.DefaultGeofenceRadiusMeters
            })
            .SingleOrDefaultAsync(cancellationToken);

        WorkSiteGeofenceReference? workSite = null;
        if (workSiteId.HasValue)
        {
            workSite = await dbContext.WorkSites
                .AsNoTracking()
                .Where(site =>
                    site.CompanyId == companyId
                    && site.Id == workSiteId.Value)
                .Select(site => new WorkSiteGeofenceReference(
                    site.Id,
                    site.Latitude,
                    site.Longitude,
                    site.GeofenceRadiusMeters))
                .SingleOrDefaultAsync(cancellationToken);

            if (workSite is null)
            {
                return null;
            }
        }

        return new GeofenceValidationReference(
            settings?.RequireGeolocation ?? false,
            settings?.GeofenceMode ?? GeofenceMode.Disabled,
            settings?.DefaultGeofenceRadiusMeters ?? 0,
            workSite);
    }
}
