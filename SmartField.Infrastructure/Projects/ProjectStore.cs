using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.Projects;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Projects;

public sealed class ProjectStore : IProjectStore
{
    private readonly SmartFieldDbContext dbContext;

    public ProjectStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken)
    {
        return await BuildSearchQuery(companyId, search)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<ProjectDto> BuildSearchQuery(
        Guid companyId,
        string? search)
    {
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(project =>
                project.Code.Contains(search)
                || project.Name.Contains(search)
                || (project.CustomerName != null && project.CustomerName.Contains(search))
                || (project.ErpProjectCode != null && project.ErpProjectCode.Contains(search))
                || (project.ErpCostCenterCode != null && project.ErpCostCenterCode.Contains(search)));
        }

        query = query
            .OrderBy(project => project.Status)
            .ThenBy(project => project.Name)
            .ThenBy(project => project.Code);

        return Project(query, companyId);
    }

    public Task<ProjectDto?> GetAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.CompanyId == companyId
                && project.Id == projectId);

        return Project(query, companyId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Project?> FindEntityAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Projects.SingleOrDefaultAsync(
            project =>
                project.CompanyId == companyId
                && project.Id == projectId,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? projectIdToExclude,
        CancellationToken cancellationToken)
    {
        return dbContext.Projects.AnyAsync(
            project =>
                project.CompanyId == companyId
                && project.Code == code
                && (!projectIdToExclude.HasValue
                    || project.Id != projectIdToExclude.Value),
            cancellationToken);
    }

    public Task<bool> WorkSiteExistsAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken)
    {
        return dbContext.WorkSites.AnyAsync(
            workSite =>
                workSite.CompanyId == companyId
                && workSite.Id == workSiteId,
            cancellationToken);
    }

    public void Add(Project project)
    {
        dbContext.Projects.Add(project);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
                && sqlException.Message.Contains(
                    "IX_Projects_CompanyId_Code",
                    StringComparison.Ordinal))
        {
            throw new ProjectCodeConflictException(
                "Já existe um projeto com este código na empresa.",
                exception);
        }
    }

    private IQueryable<ProjectDto> Project(
        IQueryable<Project> projects,
        Guid companyId)
    {
        return projects.Select(project => new ProjectDto(
            project.Id,
            project.Code,
            project.Name,
            project.ProjectType.ToString(),
            project.Status.ToString(),
            project.CustomerName,
            project.WorkSiteId,
            project.WorkSiteId.HasValue
                ? dbContext.WorkSites
                    .Where(workSite =>
                        workSite.CompanyId == companyId
                        && workSite.Id == project.WorkSiteId.Value)
                    .Select(workSite => workSite.Name)
                    .SingleOrDefault()
                : null,
            project.StartDate,
            project.EndDate,
            project.ErpProjectCode,
            project.ErpCostCenterCode,
            project.CreatedAtUtc,
            project.UpdatedAtUtc));
    }
}
