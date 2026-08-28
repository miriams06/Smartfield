namespace SmartField.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobilePhone { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? DefaultWorkSiteId { get; set; }

    public string? ExternalSystem { get; set; }

    public string? ExternalId { get; set; }

    public string? ErpEmployeeCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
