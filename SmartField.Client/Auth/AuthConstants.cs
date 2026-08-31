namespace SmartField.Client.Auth;

public static class AuthConstants
{
    public const string AdminRole = "Admin";
    public const string ManagerRole = "Manager";
    public const string EmployeeRole = "Employee";
    public const string BackofficeRoles = $"{AdminRole},{ManagerRole}";

    public const string CompanyIdClaim = "company_id";
    public const string EmployeeIdClaim = "employee_id";
}
