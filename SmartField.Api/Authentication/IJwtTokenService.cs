using SmartField.Infrastructure.Identity;

namespace SmartField.Api.Authentication;

public interface IJwtTokenService
{
    GeneratedJwtToken CreateToken(ApplicationUser user, IEnumerable<string> roles);
}
