namespace SmartField.Application.Projects;

public interface IProjectService
{
    Task<ProjectResult<IReadOnlyList<ProjectDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken);

    Task<ProjectResult<ProjectDto>> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken);

    Task<ProjectResult<ProjectDto>> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken);

    Task<ProjectResult<ProjectDto>> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken);
}
