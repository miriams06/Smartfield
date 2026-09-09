using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartField.Api.Authentication;
using SmartField.Api.Controllers;
using SmartField.Application.Abstractions;
using SmartField.Application.Audit;
using SmartField.Application.Employees;
using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Audit;
using SmartField.Infrastructure.Employees;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Outbox;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Tests;

public class ActiveAccountValidatorTests
{
    [Fact]
    public async Task TokenValidated_MissingIdentity_RejectsWithoutDatabaseAccess()
    {
        await using var db = new SmartFieldDbContext(new DbContextOptionsBuilder<SmartFieldDbContext>().Options);
        var context = TokenContext(new ClaimsPrincipal(new ClaimsIdentity()));
        await new ActiveAccountValidator(db).TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Deactivation_BlocksLoginAndExistingClaims_ForEveryLinkedRole_AndPreservesAuditAndHistory()
    {
        await WithDatabaseAsync(async (db, users, services) =>
        {
            var company = await db.Companies.FirstAsync();
            db.CurrentCompanyId = company.Id;
            var currentCompany = new CompanyProvider(company.Id);
            var audit = new AuditService(new AuditStore(db), currentCompany);
            var validator = new ActiveAccountValidator(db);
            var auth = new AuthController(users, new TestTokenService(), audit, TimeProvider.System, validator);
            var service = new EmployeeService(new EmployeeStore(db), currentCompany,
                new IntegrationOutboxService(new IntegrationOutboxStore(db)), TimeProvider.System);

            foreach (var role in SmartFieldRoles.All)
            {
                var employee = new Employee { CompanyId = company.Id, EmployeeNumber = role, Name = role };
                db.Employees.Add(employee);
                var history = new AttendanceEvent { CompanyId = company.Id, EmployeeId = employee.Id,
                    ClientEventId = Guid.NewGuid(), ServerTimestampUtc = DateTimeOffset.UtcNow };
                db.AttendanceEvents.Add(history);
                await db.SaveChangesAsync();
                var password = $"Aa1!{Guid.NewGuid():N}";
                var user = new ApplicationUser { CompanyId = company.Id, EmployeeId = employee.Id,
                    Email = $"{role}@example.test", UserName = $"{role}@example.test" };
                Assert.True((await users.CreateAsync(user, password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
                var oldPrincipal = Principal(user, role);
                Assert.IsType<OkObjectResult>((await auth.Login(new(user.Email, password), default)).Result);
                Assert.Null((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);

                var controller = new EmployeesController(service, audit, users, db, TimeProvider.System)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = oldPrincipal } } };
                Assert.IsType<OkObjectResult>((await controller.Update(employee.Id,
                    new(employee.EmployeeNumber, employee.Name, null, null, false, null, user.Id, null), default)).Result);
                Assert.IsType<UnauthorizedResult>((await auth.Login(new(user.Email, password), default)).Result);
                Assert.NotNull((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);
                Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id));
                Assert.True(await db.AttendanceEvents.AnyAsync(x => x.Id == history.Id));
                var entry = await db.AuditLogs.SingleAsync(x => x.EntityId == employee.Id && x.Action == "Updated");
                Assert.True(JsonDocument.Parse(entry.OldValues!).RootElement.GetProperty("IsActive").GetBoolean());
                Assert.False(JsonDocument.Parse(entry.NewValues!).RootElement.GetProperty("IsActive").GetBoolean());

                employee.IsActive = true;
                await db.SaveChangesAsync();
                Assert.IsType<OkObjectResult>((await auth.Login(new(user.Email, password), default)).Result);
                user.IsActive = false;
                await db.SaveChangesAsync();
                Assert.IsType<UnauthorizedResult>((await auth.Login(new(user.Email, password), default)).Result);
                Assert.NotNull((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);
            }
        });
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Validation_RejectsForeignEmployeeAndStaleAssociations_ButAllowsUnlinkedAdmin()
    {
        await WithDatabaseAsync(async (db, users, services) =>
        {
            var company = await db.Companies.FirstAsync();
            var other = new Company { Code = "OTHER", Name = "Other" };
            db.Companies.Add(other);
            var foreignEmployee = new Employee { CompanyId = other.Id, EmployeeNumber = "OTHER", Name = "Other" };
            db.Employees.Add(foreignEmployee);
            await db.SaveChangesAsync();
            var user = new ApplicationUser { CompanyId = company.Id, UserName = "admin@example.test" };
            Assert.True((await users.CreateAsync(user)).Succeeded);
            var validator = new ActiveAccountValidator(db);
            var oldPrincipal = Principal(user, SmartFieldRoles.Admin);
            Assert.Null((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);
            Assert.False(await validator.IsActiveAsync(user.Id, other.Id, null, default));
            user.EmployeeId = foreignEmployee.Id;
            await db.SaveChangesAsync();
            Assert.NotNull((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);
            Assert.False(await validator.IsActiveAsync(user.Id, company.Id, foreignEmployee.Id, default));
            user.EmployeeId = null;
            await db.SaveChangesAsync();
            var stalePrincipal = Principal(user, SmartFieldRoles.Employee);
            ((ClaimsIdentity)stalePrincipal.Identity!).AddClaim(new(SmartFieldClaimTypes.EmployeeId, foreignEmployee.Id.ToString()));
            Assert.NotNull((await ValidateAsync(validator, stalePrincipal)).Result?.Failure);
            db.Users.Remove(user);
            await db.SaveChangesAsync();
            Assert.NotNull((await ValidateAsync(validator, oldPrincipal)).Result?.Failure);
        });
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task BearerAuthentication_RejectsPreviouslyIssuedUnexpiredTokenAfterDeactivation()
    {
        await WithDatabaseAsync(async (db, users, services) =>
        {
            var company = await db.Companies.FirstAsync();
            var employee = new Employee { CompanyId = company.Id, EmployeeNumber = "JWT", Name = "JWT" };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            var user = new ApplicationUser { CompanyId = company.Id, EmployeeId = employee.Id, UserName = "jwt@example.test" };
            Assert.True((await users.CreateAsync(user)).Succeeded);
            var parameters = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Bearer").TokenValidationParameters;
            var token = new JwtSecurityToken(issuer: parameters.ValidIssuer, audience: parameters.ValidAudience,
                claims: Principal(user, SmartFieldRoles.Employee).Claims, expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(parameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256));
            var encoded = new JwtSecurityTokenHandler().WriteToken(token);
            async Task<AuthenticateResult> AuthenticateAsync()
            {
                await using var requestScope = services.CreateAsyncScope();
                var http = new DefaultHttpContext { RequestServices = requestScope.ServiceProvider };
                http.Request.Headers.Authorization = $"Bearer {encoded}";
                return await http.AuthenticateAsync("Bearer");
            }
            Assert.True((await AuthenticateAsync()).Succeeded);
            employee.IsActive = false;
            await db.SaveChangesAsync();
            // The signature and expiration remain valid; the database status rejects access.
            new JwtSecurityTokenHandler().ValidateToken(encoded, parameters, out _);
            Assert.NotNull((await AuthenticateAsync()).Failure);
        });
    }

    private static ClaimsPrincipal Principal(ApplicationUser user, string role)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(SmartFieldClaimTypes.CompanyId, user.CompanyId.ToString()), new(ClaimTypes.Role, role) };
        if (user.EmployeeId.HasValue) claims.Add(new(SmartFieldClaimTypes.EmployeeId, user.EmployeeId.ToString()!));
        return new(new ClaimsIdentity(claims, "Bearer"));
    }

    private static TokenValidatedContext TokenContext(ClaimsPrincipal principal) => new(
        new DefaultHttpContext(), new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)),
        new JwtBearerOptions()) { Principal = principal };

    private static async Task<TokenValidatedContext> ValidateAsync(ActiveAccountValidator validator, ClaimsPrincipal principal)
    {
        var context = TokenContext(principal);
        await validator.TokenValidated(context);
        return context;
    }

    private static async Task WithDatabaseAsync(Func<SmartFieldDbContext, UserManager<ApplicationUser>, IServiceProvider, Task> test)
    {
        var connection = new SqlConnectionStringBuilder(SqlServerIntegrationTestConfiguration.ConnectionString)
        { InitialCatalog = $"SmartField_ActiveAccount_{Guid.NewGuid():N}" };
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddScoped<ActiveAccountValidator>();
        var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        registrations.AddAuthentication("Bearer").AddJwtBearer(options =>
        {
            options.EventsType = typeof(ActiveAccountValidator);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = "SmartField.Tests", ValidAudience = "SmartField.Tests",
                IssuerSigningKey = signingKey, ValidateIssuerSigningKey = true,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        registrations.AddDbContext<SmartFieldDbContext>(options => options.UseSqlServer(connection.ConnectionString));
        registrations.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SmartFieldDbContext>();
        await using var provider = registrations.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            await test(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), scope.ServiceProvider);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class CompanyProvider(Guid companyId) : ICurrentCompanyProvider
    {
        public Guid? CompanyId => companyId;
    }

    private sealed class TestTokenService : IJwtTokenService
    {
        public GeneratedJwtToken CreateToken(ApplicationUser user, IEnumerable<string> roles) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));
    }
}
