using SmartField.Domain.Enums;

namespace SmartField.Domain.Entities;

public class IntegrationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Payload { get; set; } = string.Empty;

    public IntegrationStatus Status { get; set; } = IntegrationStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptUtc { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }
}
