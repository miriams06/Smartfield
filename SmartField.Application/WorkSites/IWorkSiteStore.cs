using SmartField.Domain.Entities;

namespace SmartField.Application.WorkSites;

public interface IWorkSiteStore
{
    Task<IReadOnlyList<WorkSiteDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<WorkSiteDto?> GetAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken);

    Task<WorkSite?> FindEntityAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? workSiteIdToExclude,
        CancellationToken cancellationToken);

    void Add(WorkSite workSite);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
