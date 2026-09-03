using Microsoft.EntityFrameworkCore;
using SmartField.Application.Geolocation;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Geolocation;

public sealed class GeofenceSettingsStore : IGeofenceSettingsStore
{
    private readonly SmartFieldDbContext dbContext;

    public GeofenceSettingsStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task<CompanySettings?> FindAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return dbContext.CompanySettings.SingleOrDefaultAsync(
            settings => settings.CompanyId == companyId,
            cancellationToken);
    }

    public void Add(CompanySettings settings)
    {
        dbContext.CompanySettings.Add(settings);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
