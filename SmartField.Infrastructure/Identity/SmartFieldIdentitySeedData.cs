using Microsoft.AspNetCore.Identity;

namespace SmartField.Infrastructure.Identity;

public static class SmartFieldIdentitySeedData
{
    public static readonly Guid AdminRoleId = Guid.Parse("29f8ad2a-0f22-48c9-9c40-b4ed01e9cc98");
    public static readonly Guid ManagerRoleId = Guid.Parse("327c4d98-6d8b-47a3-a64b-109c2ef3318b");
    public static readonly Guid EmployeeRoleId = Guid.Parse("3db44f13-4e29-46b4-a2b9-813c89ef9b3f");

    public static IdentityRole<Guid>[] Roles =>
    [
        CreateRole(AdminRoleId, SmartFieldRoles.Admin),
        CreateRole(ManagerRoleId, SmartFieldRoles.Manager),
        CreateRole(EmployeeRoleId, SmartFieldRoles.Employee)
    ];

    private static IdentityRole<Guid> CreateRole(Guid id, string name)
    {
        return new IdentityRole<Guid>
        {
            Id = id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = id.ToString()
        };
    }
}
