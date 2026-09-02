namespace SmartField.Application.Audit;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetAsync(CancellationToken cancellationToken);

    void Add(
        Guid companyId,
        Guid? userId,
        string entityType,
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        DateTimeOffset timestampUtc);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
