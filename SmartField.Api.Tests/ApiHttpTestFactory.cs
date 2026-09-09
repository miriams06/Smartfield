using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartField.Api.Authentication;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Tests;

internal sealed class ApiHttpTestFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
        SqlServerIntegrationTestConfiguration.ConnectionString)
    { InitialCatalog = $"SmartField_Http_{Guid.NewGuid():N}" }.ConnectionString;
    private readonly string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public string Password { get; } = $"Aa1!{Guid.NewGuid():N}";
    public Guid CompanyA { get; } = Guid.NewGuid();
    public Guid CompanyB { get; } = Guid.NewGuid();
    public Guid EmployeeA { get; } = Guid.NewGuid();
    public Guid EmployeeB { get; } = Guid.NewGuid();
    public Guid SiteA { get; } = Guid.NewGuid();
    public Guid SiteB { get; } = Guid.NewGuid();
    public Guid ProjectB { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", signingKey);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = signingKey,
                ["Jwt:Issuer"] = "SmartField.Http.Tests",
                ["Jwt:Audience"] = "SmartField.Http.Tests",
                ["ConnectionStrings:SmartField"] = connectionString
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SmartFieldDbContext>>();
            services.AddDbContext<SmartFieldDbContext>(options => options.UseSqlServer(connectionString));
        });
    }

    public static async Task<ApiHttpTestFactory> StartAsync()
    {
        var factory = new ApiHttpTestFactory();
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>();
            // Assert the replacement before any schema or data operation.
            Assert.Equal(factory.connectionString, db.Database.GetConnectionString());
            await db.Database.MigrateAsync();
            db.Companies.AddRange(
                new Company { Id = factory.CompanyA, Code = "HTTP-A", Name = "HTTP Company A" },
                new Company { Id = factory.CompanyB, Code = "HTTP-B", Name = "HTTP Company B" });
            db.CompanySettings.AddRange(
                new CompanySettings { CompanyId = factory.CompanyA, AllowBreaks = true, DefaultGeofenceRadiusMeters = 100 },
                new CompanySettings { CompanyId = factory.CompanyB, AllowBreaks = true, DefaultGeofenceRadiusMeters = 100 });
            db.WorkSites.AddRange(
                new WorkSite { Id = factory.SiteA, CompanyId = factory.CompanyA, Code = "A-SITE", Name = "A Site", Latitude = 38.72m, Longitude = -9.14m, GeofenceRadiusMeters = 100 },
                new WorkSite { Id = factory.SiteB, CompanyId = factory.CompanyB, Code = "B-SITE", Name = "B Site", Latitude = 41.15m, Longitude = -8.61m, GeofenceRadiusMeters = 100 });
            db.Employees.AddRange(
                new Employee { Id = factory.EmployeeA, CompanyId = factory.CompanyA, EmployeeNumber = "A001", Name = "A Employee", DefaultWorkSiteId = factory.SiteA },
                new Employee { Id = factory.EmployeeB, CompanyId = factory.CompanyB, EmployeeNumber = "B001", Name = "B Employee", DefaultWorkSiteId = factory.SiteB });
            db.Projects.Add(new Project { Id = factory.ProjectB, CompanyId = factory.CompanyB, Code = "B-PROJECT", Name = "B Project" });
            await db.SaveChangesAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var (email, company, employee, role, active) in new[]
            {
                ("employee.a@example.test", factory.CompanyA, (Guid?)factory.EmployeeA, SmartFieldRoles.Employee, true),
                ("employee.b@example.test", factory.CompanyB, (Guid?)factory.EmployeeB, SmartFieldRoles.Employee, true),
                ("manager.a@example.test", factory.CompanyA, (Guid?)null, SmartFieldRoles.Manager, true),
                ("inactive@example.test", factory.CompanyA, (Guid?)null, SmartFieldRoles.Employee, false)
            })
            {
                var user = new ApplicationUser { UserName = email, Email = email, CompanyId = company, EmployeeId = employee, IsActive = active };
                Assert.True((await users.CreateAsync(user, factory.Password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }
            return factory;
        }
        catch
        {
            await factory.DisposeAsync();
            throw;
        }
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = NewClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    public async Task WithDatabaseAsync(Func<SmartFieldDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally
        {
            // Only this factory's generated database can be deleted, never the configured catalog.
            await using var db = new SmartFieldDbContext(new DbContextOptionsBuilder<SmartFieldDbContext>()
                .UseSqlServer(connectionString).Options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
