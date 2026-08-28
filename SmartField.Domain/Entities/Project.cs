using SmartField.Domain.Enums;

namespace SmartField.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ProjectType ProjectType { get; set; } = ProjectType.Other;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public string? CustomerName { get; set; }

    public Guid? WorkSiteId { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ExternalSystem { get; set; }

    public string? ExternalId { get; set; }

    public string? ErpProjectCode { get; set; }

    public string? ErpCostCenterCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
