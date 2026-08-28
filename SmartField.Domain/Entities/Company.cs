namespace SmartField.Domain.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Nif { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "Europe/Lisbon";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
