using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartField.Infrastructure.Identity;

namespace SmartField.Api.Authentication;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions options;
    private readonly JwtSigningKey signingKey;
    private readonly TimeProvider timeProvider;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        JwtSigningKey signingKey,
        TimeProvider timeProvider)
    {
        this.options = options.Value;
        this.signingKey = signingKey;
        this.timeProvider = timeProvider;
    }

    public GeneratedJwtToken CreateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(options.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(SmartFieldClaimTypes.CompanyId, user.CompanyId.ToString())
        };

        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim(SmartFieldClaimTypes.EmployeeId, user.EmployeeId.Value.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(signingKey.SecurityKey, SecurityAlgorithms.HmacSha256));

        return new GeneratedJwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
