namespace SmartField.Application.Audit;

public sealed record AuditLogDto(
    Guid Id,
    Guid CompanyId,
    Guid? UserId,
    string? UserEmail,
    string EntityType,
    Guid EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    DateTimeOffset TimestampUtc);
