namespace SmartField.Domain.Entities;

public class WorkSite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? GeofenceRadiusMeters { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ExternalSystem { get; set; }

    public string? ExternalId { get; set; }

    public string? ErpCostCenterCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
