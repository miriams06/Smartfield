namespace SmartField.Application.WorkSites;

public interface IWorkSiteService
{
    Task<WorkSiteResult<IReadOnlyList<WorkSiteDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken);

    Task<WorkSiteResult<WorkSiteDto>> GetAsync(
        Guid workSiteId,
        CancellationToken cancellationToken);

    Task<WorkSiteResult<WorkSiteDto>> CreateAsync(
        CreateWorkSiteRequest request,
        CancellationToken cancellationToken);

    Task<WorkSiteResult<WorkSiteDto>> UpdateAsync(
        Guid workSiteId,
        UpdateWorkSiteRequest request,
        CancellationToken cancellationToken);
}
