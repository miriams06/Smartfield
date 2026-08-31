namespace SmartField.Infrastructure.Identity;

public static class SmartFieldRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static readonly string[] All =
    [
        Admin,
        Manager,
        Employee
    ];
}
