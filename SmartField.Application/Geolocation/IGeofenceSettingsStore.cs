using SmartField.Domain.Entities;

namespace SmartField.Application.Geolocation;

public interface IGeofenceSettingsStore
{
    Task<CompanySettings?> FindAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    void Add(CompanySettings settings);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
