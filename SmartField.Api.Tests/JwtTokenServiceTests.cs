using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartField.Api.Authentication;
using SmartField.Infrastructure.Identity;
using System.IdentityModel.Tokens.Jwt;

namespace SmartField.Api.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_IncludesCompanyEmployeeAndRolesClaims()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var signingKey = JwtSigningKey.Create(CreateConfiguration(), new TestEnvironment());
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "SmartField.Tests",
                Audience = "SmartField.Client.Tests",
                ExpirationMinutes = 60
            }),
            signingKey,
            TimeProvider.System);
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@smartfield.local",
            CompanyId = companyId,
            EmployeeId = employeeId
        };

        var token = service.CreateToken(user, [SmartFieldRoles.Admin]);

        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "SmartField.Tests",
                ValidateAudience = true,
                ValidAudience = "SmartField.Client.Tests",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey.SecurityKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            },
            out _);

        Assert.Equal(user.Id, principal.GetRequiredUserId());
        Assert.Equal(companyId, principal.GetRequiredCompanyId());
        Assert.Equal(employeeId, principal.GetEmployeeId());
        Assert.Contains(principal.FindAll(ClaimTypes.Role), claim => claim.Value == SmartFieldRoles.Admin);
    }

    [Fact]
    public void EmployeeRole_IsNotABackofficeRole()
    {
        string[] backofficeRoles = [SmartFieldRoles.Admin, SmartFieldRoles.Manager];

        Assert.DoesNotContain(SmartFieldRoles.Employee, backofficeRoles);
    }

    private static IConfiguration CreateConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "smartfield-tests-signing-key-with-at-least-32-bytes"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "SmartField.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
