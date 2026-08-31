using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.WorkSites;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.WorkSites;

public sealed class WorkSiteStore : IWorkSiteStore
{
    private readonly SmartFieldDbContext dbContext;

    public WorkSiteStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WorkSiteDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken)
    {
        return await BuildSearchQuery(companyId, search)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<WorkSiteDto> BuildSearchQuery(
        Guid companyId,
        string? search)
    {
        var query = dbContext.WorkSites
            .AsNoTracking()
            .Where(workSite => workSite.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(workSite =>
                workSite.Code.Contains(search)
                || workSite.Name.Contains(search)
                || (workSite.Address != null && workSite.Address.Contains(search))
                || (workSite.ErpCostCenterCode != null && workSite.ErpCostCenterCode.Contains(search)));
        }

        query = query
            .OrderByDescending(workSite => workSite.IsActive)
            .ThenBy(workSite => workSite.Name)
            .ThenBy(workSite => workSite.Code);

        return Project(query);
    }

    public Task<WorkSiteDto?> GetAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkSites
            .AsNoTracking()
            .Where(workSite =>
                workSite.CompanyId == companyId
                && workSite.Id == workSiteId);

        return Project(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkSite?> FindEntityAsync(
        Guid companyId,
        Guid workSiteId,
        CancellationToken cancellationToken)
    {
        return dbContext.WorkSites.SingleOrDefaultAsync(
            workSite =>
                workSite.CompanyId == companyId
                && workSite.Id == workSiteId,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? workSiteIdToExclude,
        CancellationToken cancellationToken)
    {
        return dbContext.WorkSites.AnyAsync(
            workSite =>
                workSite.CompanyId == companyId
                && workSite.Code == code
                && (!workSiteIdToExclude.HasValue
                    || workSite.Id != workSiteIdToExclude.Value),
            cancellationToken);
    }

    public void Add(WorkSite workSite)
    {
        dbContext.WorkSites.Add(workSite);
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
                    "IX_WorkSites_CompanyId_Code",
                    StringComparison.Ordinal))
        {
            throw new WorkSiteCodeConflictException(
                "Já existe um local de trabalho com este código na empresa.",
                exception);
        }
    }

    private static IQueryable<WorkSiteDto> Project(IQueryable<WorkSite> workSites)
    {
        return workSites.Select(workSite => new WorkSiteDto(
            workSite.Id,
            workSite.Code,
            workSite.Name,
            workSite.Address,
            workSite.Latitude,
            workSite.Longitude,
            workSite.GeofenceRadiusMeters,
            workSite.IsActive,
            workSite.ErpCostCenterCode,
            workSite.CreatedAtUtc,
            workSite.UpdatedAtUtc));
    }
}
