using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SmartField.Api.Authentication;

public sealed class JwtSigningKey
{
    private JwtSigningKey(SymmetricSecurityKey securityKey)
    {
        SecurityKey = securityKey;
    }

    public SymmetricSecurityKey SecurityKey { get; }

    public static JwtSigningKey Create(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var options = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        if (!string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return new JwtSigningKey(new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(options.SigningKey)));
        }

        if (environment.IsDevelopment())
        {
            return new JwtSigningKey(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        }

        throw new InvalidOperationException("Jwt:SigningKey must be configured outside source control.");
    }
}
