using SmartField.Domain.Entities;

namespace SmartField.Application.Projects;

public interface IProjectStore
{
    Task<IReadOnlyList<ProjectDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<ProjectDto?> GetAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken);

    Task<Project?> FindEntityAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? projectIdToExclude,
        CancellationToken cancellationToken);

    Task<bool> WorkSiteExistsAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken);

    void Add(Project project);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
