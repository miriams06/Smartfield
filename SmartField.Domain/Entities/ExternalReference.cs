namespace SmartField.Domain.Entities;

public class ExternalReference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string SystemName { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid LocalEntityId { get; set; }

    public string ExternalEntityId { get; set; } = string.Empty;

    public string? ExternalCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
