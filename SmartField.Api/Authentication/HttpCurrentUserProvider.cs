using System.Security.Claims;
using SmartField.Application.Abstractions;

namespace SmartField.Api.Authentication;

public sealed class HttpCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => GetGuidClaim(ClaimTypes.NameIdentifier);

    public Guid? EmployeeId => GetGuidClaim(SmartFieldClaimTypes.EmployeeId);

    private Guid? GetGuidClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
