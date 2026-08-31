using System.Security.Claims;

namespace SmartField.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        return GetRequiredGuidClaim(user, ClaimTypes.NameIdentifier);
    }

    public static Guid GetRequiredCompanyId(this ClaimsPrincipal user)
    {
        return GetRequiredGuidClaim(user, SmartFieldClaimTypes.CompanyId);
    }

    public static Guid? GetEmployeeId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(SmartFieldClaimTypes.EmployeeId);
        return Guid.TryParse(value, out var employeeId) ? employeeId : null;
    }

    private static Guid GetRequiredGuidClaim(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirstValue(claimType);

        if (!Guid.TryParse(value, out var id))
        {
            throw new InvalidOperationException($"Authenticated user is missing required claim '{claimType}'.");
        }

        return id;
    }
}
