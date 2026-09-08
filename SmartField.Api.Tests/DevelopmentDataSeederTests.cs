using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SmartField.Api.Authentication;
using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Employees;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;
using SmartField.Infrastructure.Projects;
using SmartField.Infrastructure.WorkSites;

namespace SmartField.Api.Tests;

public class DevelopmentDataSeederTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Seed_OutsideDevelopment_DoesNotResolveDatabaseOrIdentity(string environment)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await DevelopmentDataSeeder.SeedAsync(services, new TestEnvironment(environment), Configuration());
    }

    [Fact]
    public async Task Seed_WithoutPasswords_DoesNotResolveDatabaseOrIdentity()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await DevelopmentDataSeeder.SeedAsync(services, new TestEnvironment("Development"), new ConfigurationBuilder().Build());
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Seed_IsIdempotent_AndIsolatesBothCompaniesUsingAuthenticatedCompany()
    {
        await WithDatabaseAsync(async services =>
        {
            var db = services.GetRequiredService<SmartFieldDbContext>();
            db.Roles.RemoveRange(await db.Roles.ToListAsync());
            await db.SaveChangesAsync();
            var config = Configuration();
            await SeedAsync(services, config);
            var accounts = await db.Users.OrderBy(x => x.Email).ToListAsync();
            var hashes = accounts.Select(x => x.PasswordHash).ToArray();
            var employeeIds = await db.Employees.IgnoreQueryFilters().Select(x => x.Id).ToListAsync();
            var site = await db.WorkSites.IgnoreQueryFilters().SingleAsync(x => x.Code == "SYS-SEDE");
            site.Name = "Sede editada manualmente";
            await db.SaveChangesAsync();
            await SeedAsync(services, Configuration()); // Different generated passwords must not reset accounts.
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Companies.CountAsync());
            Assert.Equal(2, await db.CompanySettings.IgnoreQueryFilters().CountAsync());
            Assert.Equal(3, await db.Roles.CountAsync());
            Assert.Equal(6, await db.Users.CountAsync());
            Assert.Equal(5, await db.Employees.IgnoreQueryFilters().CountAsync());
            Assert.Equal(4, await db.WorkSites.IgnoreQueryFilters().CountAsync());
            Assert.Equal(3, await db.Projects.IgnoreQueryFilters().CountAsync());
            Assert.Equal(hashes, await db.Users.OrderBy(x => x.Email).Select(x => x.PasswordHash).ToArrayAsync());
            Assert.Equal(employeeIds.Order(), (await db.Employees.IgnoreQueryFilters().Select(x => x.Id).ToListAsync()).Order());
            Assert.Equal("Sede editada manualmente", (await db.WorkSites.IgnoreQueryFilters().SingleAsync(x => x.Id == site.Id)).Name);

            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var sys = await db.Companies.SingleAsync(x => x.Code == "SYS-DEMO");
            var avac = await db.Companies.SingleAsync(x => x.Code == "AVAC-DEMO");
            Assert.Equal("Sysprime Demo", sys.Name);
            Assert.Equal("AVAC Demo", avac.Name);
            Assert.All(await db.Companies.ToListAsync(), x => Assert.Equal("Europe/Lisbon", x.TimeZone));
            var expected = new[]
            {
                ("admin@smartfield.local", sys.Id, "Admin", (string?)null, (string?)null),
                ("manager@smartfield.local", sys.Id, "Manager", "MGR001", "Marta Ferreira"),
                ("joao.silva@smartfield.local", sys.Id, "Employee", "FUNC001", "João Silva"),
                ("maria.costa@smartfield.local", sys.Id, "Employee", "FUNC002", "Maria Costa"),
                ("manager.avac@smartfield.local", avac.Id, "Manager", "MGR001", "Carlos Sousa"),
                ("tecnico.avac@smartfield.local", avac.Id, "Employee", "TEC001", "Técnico AVAC")
            };
            foreach (var (email, companyId, role, number, name) in expected)
            {
                var user = await users.FindByEmailAsync(email);
                Assert.NotNull(user);
                Assert.Equal(companyId, user.CompanyId);
                Assert.Equal(role, Assert.Single(await users.GetRolesAsync(user)));
                Assert.True(await users.CheckPasswordAsync(user, config[$"Seed:{role}Password"]!));
                if (number is null) continue;
                var employee = await db.Employees.IgnoreQueryFilters().SingleAsync(x => x.Id == user.EmployeeId);
                Assert.Equal(companyId, employee.CompanyId);
                Assert.Equal(number, employee.EmployeeNumber);
                Assert.Equal(name, employee.Name);
                var defaultSite = await db.WorkSites.IgnoreQueryFilters().SingleAsync(x => x.Id == employee.DefaultWorkSiteId);
                Assert.Equal(companyId, defaultSite.CompanyId);
            }
            Assert.Equal(ProjectType.Construction, (await db.Projects.IgnoreQueryFilters().SingleAsync(x => x.Code == "OBR-001")).ProjectType);
            Assert.All(await db.Projects.IgnoreQueryFilters().Where(x => x.Code.StartsWith("MAN-")).ToListAsync(), x => Assert.Equal(ProjectType.Maintenance, x.ProjectType));

            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            var currentCompany = services.GetRequiredService<ICurrentCompanyProvider>();
            foreach (var user in await db.Users.ToListAsync())
            {
                accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(SmartFieldClaimTypes.CompanyId, user.CompanyId.ToString()) }, "Test")) };
                var ownId = currentCompany.CompanyId!.Value;
                var otherId = ownId == sys.Id ? avac.Id : sys.Id;
                Assert.NotEmpty(await db.Employees.ToListAsync());
                Assert.All(await db.Employees.ToListAsync(), x => Assert.Equal(ownId, x.CompanyId));
                Assert.All(await db.WorkSites.ToListAsync(), x => Assert.Equal(ownId, x.CompanyId));
                Assert.All(await db.Projects.ToListAsync(), x => Assert.Equal(ownId, x.CompanyId));
                Assert.Equal(ownId, (await db.CompanySettings.SingleAsync()).CompanyId);
                var foreignEmployee = await db.Employees.IgnoreQueryFilters().FirstAsync(x => x.CompanyId == otherId);
                var foreignSite = await db.WorkSites.IgnoreQueryFilters().FirstAsync(x => x.CompanyId == otherId);
                var foreignProject = await db.Projects.IgnoreQueryFilters().FirstAsync(x => x.CompanyId == otherId);
                Assert.Null(await new EmployeeStore(db).GetAsync(ownId, foreignEmployee.Id, default));
                Assert.Null(await new WorkSiteStore(db).GetAsync(ownId, foreignSite.Id, default));
                Assert.Null(await new ProjectStore(db).GetAsync(ownId, foreignProject.Id, default));
                Assert.Empty(await new EmployeeStore(db).SearchAsync(otherId, null, default));
                Assert.Empty(await new WorkSiteStore(db).SearchAsync(otherId, null, default));
                Assert.Empty(await new ProjectStore(db).SearchAsync(otherId, null, default));
            }
        });
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Seed_PreservesLegacyAccountsEmployeeIdsAndHistory()
    {
        await WithDatabaseAsync(async services =>
        {
            var db = services.GetRequiredService<SmartFieldDbContext>();
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var original = await db.Employees.IgnoreQueryFilters().SingleAsync();
            var mobile = new Employee { CompanyId = original.CompanyId, EmployeeNumber = "FUNC002", Name = "Funcionario Mobile Demo", Email = "employee@smartfield.local", CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Employees.Add(mobile);
            var attendance = new AttendanceEvent { CompanyId = original.CompanyId, EmployeeId = original.Id, ClientEventId = Guid.NewGuid(), ServerTimestampUtc = DateTimeOffset.UtcNow, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.AttendanceEvents.Add(attendance);
            await db.SaveChangesAsync();
            var config = Configuration();
            foreach (var (email, employeeId, role) in new[] { ("admin@smartfield.local", original.Id, "Admin"), ("employee@smartfield.local", mobile.Id, "Employee") })
            {
                var user = new ApplicationUser { CompanyId = original.CompanyId, EmployeeId = employeeId, UserName = email, Email = email };
                Assert.True((await users.CreateAsync(user, config[$"Seed:{role}Password"]!)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            }
            await SeedAsync(services, config);
            await SeedAsync(services, config);
            db.ChangeTracker.Clear();
            Assert.Equal("ADMIN-DEMO", (await db.Employees.IgnoreQueryFilters().SingleAsync(x => x.Id == original.Id)).EmployeeNumber);
            Assert.Equal("MOBILE-DEMO", (await db.Employees.IgnoreQueryFilters().SingleAsync(x => x.Id == mobile.Id)).EmployeeNumber);
            Assert.Equal(original.Id, (await users.FindByEmailAsync("admin@smartfield.local"))!.EmployeeId);
            Assert.Equal(mobile.Id, (await users.FindByEmailAsync("employee@smartfield.local"))!.EmployeeId);
            Assert.Equal(original.Id, (await db.AttendanceEvents.IgnoreQueryFilters().SingleAsync()).EmployeeId);
            Assert.NotEqual(original.Id, (await users.FindByEmailAsync("joao.silva@smartfield.local"))!.EmployeeId);
            Assert.NotEqual(mobile.Id, (await users.FindByEmailAsync("maria.costa@smartfield.local"))!.EmployeeId);
        });
    }

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Seed_ConflictingCompany_RollsBackNewDemoData()
    {
        await WithDatabaseAsync(async services =>
        {
            var db = services.GetRequiredService<SmartFieldDbContext>();
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var other = new Company { Code = "OTHER", Name = "Other", CreatedAtUtc = DateTimeOffset.UtcNow };
            db.Companies.Add(other);
            await db.SaveChangesAsync();
            var config = Configuration();
            Assert.True((await users.CreateAsync(new ApplicationUser { CompanyId = other.Id, UserName = "joao.silva@smartfield.local", Email = "joao.silva@smartfield.local" }, config["Seed:EmployeePassword"]!)).Succeeded);
            await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync(services, config));
            db.ChangeTracker.Clear();
            Assert.False(await db.Companies.AnyAsync(x => x.Code == "AVAC-DEMO"));
            Assert.Empty(await db.WorkSites.IgnoreQueryFilters().ToListAsync());
            Assert.Single(await db.Users.ToListAsync());
        });
    }

    private static Task SeedAsync(IServiceProvider services, IConfiguration configuration) =>
        DevelopmentDataSeeder.SeedAsync(services, new TestEnvironment("Development"), configuration);

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        SmartFieldRoles.All.ToDictionary(x => $"Seed:{x}Password", _ => (string?)($"Aa1!{Guid.NewGuid():N}"))).Build();

    private static async Task WithDatabaseAsync(Func<IServiceProvider, Task> test)
    {
        var connection = new SqlConnectionStringBuilder(SqlServerIntegrationTestConfiguration.ConnectionString)
        { InitialCatalog = $"SmartField_DevelopmentSeed_{Guid.NewGuid():N}" };
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddHttpContextAccessor();
        registrations.AddScoped<ICurrentCompanyProvider, HttpCurrentCompanyProvider>();
        registrations.AddDbContext<SmartFieldDbContext>(options => options.UseSqlServer(connection.ConnectionString));
        registrations.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<SmartFieldDbContext>();
        await using var provider = registrations.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            await test(scope.ServiceProvider);
        }
        finally
        {
            // Only this test's uniquely named database is removed.
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SmartField.Api.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
