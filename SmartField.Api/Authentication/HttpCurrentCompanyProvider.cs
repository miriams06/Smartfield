using System.Security.Claims;
using SmartField.Application.Abstractions;

namespace SmartField.Api.Authentication;

public sealed class HttpCurrentCompanyProvider : ICurrentCompanyProvider
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public HttpCurrentCompanyProvider(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? CompanyId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(SmartFieldClaimTypes.CompanyId);
            return Guid.TryParse(value, out var companyId) ? companyId : null;
        }
    }
}
