using Microsoft.EntityFrameworkCore;
using SmartField.Application.Audit;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Audit;

public sealed class AuditStore : IAuditStore
{
    private readonly SmartFieldDbContext dbContext;

    public AuditStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await dbContext.AuditLogs
            .AsNoTracking()
            .Where(auditLog => auditLog.CompanyId == companyId)
            .OrderByDescending(auditLog => auditLog.TimestampUtc)
            .ThenByDescending(auditLog => auditLog.CreatedAtUtc)
            .ThenByDescending(auditLog => auditLog.Id)
            .Select(auditLog => new AuditLogDto(
                auditLog.Id,
                auditLog.CompanyId,
                auditLog.UserId,
                auditLog.UserId.HasValue
                    ? dbContext.Users
                        .Where(user =>
                            user.CompanyId == companyId
                            && user.Id == auditLog.UserId.Value)
                        .Select(user => user.Email)
                        .SingleOrDefault()
                    : null,
                auditLog.EntityType,
                auditLog.EntityId,
                auditLog.Action,
                auditLog.OldValues,
                auditLog.NewValues,
                auditLog.TimestampUtc))
            .ToListAsync(cancellationToken);
    }

    public void Add(AuditLog auditLog)
    {
        dbContext.AuditLogs.Add(auditLog);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
