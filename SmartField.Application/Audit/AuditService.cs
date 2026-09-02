using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;

namespace SmartField.Application.Audit;

public sealed class AuditService : IAuditService
{
    private readonly IAuditStore auditStore;
    private readonly ICurrentCompanyProvider currentCompanyProvider;

    public AuditService(
        IAuditStore auditStore,
        ICurrentCompanyProvider currentCompanyProvider)
    {
        this.auditStore = auditStore;
        this.currentCompanyProvider = currentCompanyProvider;
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAsync(
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        if (!companyId.HasValue)
        {
            return [];
        }

        return await auditStore.GetAsync(companyId.Value, cancellationToken);
    }

    public void Add(
        Guid companyId,
        Guid? userId,
        string entityType,
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        DateTimeOffset timestampUtc)
    {
        auditStore.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            TimestampUtc = timestampUtc.ToUniversalTime(),
            CreatedAtUtc = timestampUtc.ToUniversalTime()
        });
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        auditStore.SaveChangesAsync(cancellationToken);
}
