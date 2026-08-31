using Microsoft.AspNetCore.Identity;

namespace SmartField.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid CompanyId { get; set; }

    public Guid? EmployeeId { get; set; }

    public bool IsActive { get; set; } = true;
}
