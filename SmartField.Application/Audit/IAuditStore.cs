using SmartField.Domain.Entities;

namespace SmartField.Application.Audit;

public interface IAuditStore
{
    Task<IReadOnlyList<AuditLogDto>> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    void Add(AuditLog auditLog);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
