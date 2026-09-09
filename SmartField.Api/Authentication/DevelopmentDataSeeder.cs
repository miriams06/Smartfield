using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Authentication;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services, IHostEnvironment environment, IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return;

        var passwords = SmartFieldRoles.All.ToDictionary(role => role, role => configuration[$"Seed:{role}Password"]);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevelopmentDataSeeder));
        foreach (var (role, password) in passwords)
        {
            if (string.IsNullOrWhiteSpace(password))
                logger.LogWarning("Development demo accounts for {Role} require Seed:{Role}Password. Existing passwords are not changed.", role, role);
        }
        if (passwords.Values.All(string.IsNullOrWhiteSpace)) return;

        var db = services.GetRequiredService<SmartFieldDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var role in SmartFieldRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), "create demo role");
        }

        var sys = await CompanyAsync("SYS-DEMO", "Sysprime Demo");
        var avac = await CompanyAsync("AVAC-DEMO", "AVAC Demo");
        var sede = await WorkSiteAsync(sys.Id, "SYS-SEDE", "Sede", 38.722300m, -9.139300m);
        await WorkSiteAsync(sys.Id, "SYS-ARM", "Armazém", 38.730000m, -9.145000m);
        var obra = await WorkSiteAsync(sys.Id, "OBR-001", "Obra Porto", 41.149610m, -8.610990m);
        var avacSede = await WorkSiteAsync(avac.Id, "AVAC-SEDE", "Sede AVAC", 41.160000m, -8.620000m);
        await ProjectAsync(sys.Id, "OBR-001", "Construção Porto", ProjectType.Construction, obra.Id);
        await ProjectAsync(sys.Id, "MAN-001", "Manutenção AVAC", ProjectType.Maintenance, sede.Id);
        await ProjectAsync(avac.Id, "MAN-AVAC-001", "Contrato Manutenção Cliente Demo", ProjectType.Maintenance, avacSede.Id);

        // Keep the legacy employee IDs, user links and attendance history intact.
        await PreserveLegacyEmployeeAsync("admin@smartfield.local", "FUNC001", "ADMIN-DEMO");
        await PreserveLegacyEmployeeAsync("employee@smartfield.local", "FUNC002", "MOBILE-DEMO");
        await UserAsync(sys.Id, "admin@smartfield.local", SmartFieldRoles.Admin, null);
        await PersonAsync(sys.Id, "manager@smartfield.local", "MGR001", "Marta Ferreira", SmartFieldRoles.Manager, sede.Id);
        await PersonAsync(sys.Id, "joao.silva@smartfield.local", "FUNC001", "João Silva", SmartFieldRoles.Employee, sede.Id);
        await PersonAsync(sys.Id, "maria.costa@smartfield.local", "FUNC002", "Maria Costa", SmartFieldRoles.Employee, sede.Id);
        await PersonAsync(avac.Id, "manager.avac@smartfield.local", "MGR001", "Carlos Sousa", SmartFieldRoles.Manager, avacSede.Id);
        await PersonAsync(avac.Id, "tecnico.avac@smartfield.local", "TEC001", "Técnico AVAC", SmartFieldRoles.Employee, avacSede.Id);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        async Task<Company> CompanyAsync(string code, string name)
        {
            var company = await db.Companies.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
            if (company is null)
            {
                company = new Company { Code = code, Name = name, TimeZone = "Europe/Lisbon", CreatedAtUtc = DateTimeOffset.UtcNow };
                db.Companies.Add(company);
            }
            else if (code == "SYS-DEMO" && company.Name == "SmartField Demo")
            {
                company.Name = name;
                company.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
            if (!await db.CompanySettings.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == company.Id, cancellationToken))
                db.CompanySettings.Add(new CompanySettings { CompanyId = company.Id, DefaultGeofenceRadiusMeters = 100, CreatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
            return company;
        }

        async Task<WorkSite> WorkSiteAsync(Guid companyId, string code, string name, decimal latitude, decimal longitude)
        {
            var site = await db.WorkSites.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken);
            if (site is null)
            {
                site = new WorkSite { CompanyId = companyId, Code = code, Name = name, CreatedAtUtc = DateTimeOffset.UtcNow };
                db.WorkSites.Add(site);
            }
            // Illustrative demo positions only. Never overwrite a manually configured position.
            if (site.Latitude is null && site.Longitude is null)
            {
                site.Latitude = latitude;
                site.Longitude = longitude;
                site.GeofenceRadiusMeters ??= 200;
            }
            await db.SaveChangesAsync(cancellationToken);
            return site;
        }

        async Task ProjectAsync(Guid companyId, string code, string name, ProjectType type, Guid siteId)
        {
            if (await db.Projects.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == companyId && x.Code == code, cancellationToken)) return;
            db.Projects.Add(new Project { CompanyId = companyId, Code = code, Name = name, ProjectType = type,
                Status = ProjectStatus.Active, WorkSiteId = siteId, CreatedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }

        async Task PreserveLegacyEmployeeAsync(string email, string number, string replacement)
        {
            var user = await users.FindByEmailAsync(email);
            if (user?.EmployeeId is null) return;
            if (user.CompanyId != sys.Id) throw new InvalidOperationException("Legacy demo user belongs to another company.");
            var employee = await db.Employees.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == user.EmployeeId, cancellationToken);
            if (employee is null || employee.CompanyId != sys.Id)
                throw new InvalidOperationException("Legacy demo employee association is invalid.");
            if (employee.EmployeeNumber != number) return;
            if (await db.Employees.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == sys.Id && x.EmployeeNumber == replacement, cancellationToken))
                throw new InvalidOperationException($"Cannot preserve legacy demo employee: {replacement} is already in use.");
            employee.EmployeeNumber = replacement;
            employee.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        async Task PersonAsync(Guid companyId, string email, string number, string name, string role, Guid siteId)
        {
            var employee = await db.Employees.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.CompanyId == companyId && x.EmployeeNumber == number, cancellationToken);
            if (employee is null)
            {
                employee = new Employee { CompanyId = companyId, EmployeeNumber = number, Name = name, Email = email,
                    DefaultWorkSiteId = siteId, CreatedAtUtc = DateTimeOffset.UtcNow };
                db.Employees.Add(employee);
            }
            else
            {
                var conflictingUser = await db.Users.AnyAsync(x => x.EmployeeId == employee.Id && x.NormalizedEmail != email.ToUpperInvariant(), cancellationToken);
                if (conflictingUser || (employee.Email is not null && !string.Equals(employee.Email, email, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Demo employee {number} is already assigned to another account.");
                // Upgrade only the known unassigned records from the old demo seed.
                if (employee.Email is null && (employee.Name == "Funcionário Demo" || employee.Name == "Funcionario Mobile Demo"))
                {
                    employee.Name = name;
                    employee.Email = email;
                }
                employee.DefaultWorkSiteId ??= siteId;
            }
            await db.SaveChangesAsync(cancellationToken);
            await UserAsync(companyId, email, role, employee.Id);
        }

        async Task UserAsync(Guid companyId, string email, string role, Guid? employeeId)
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(passwords[role])) return;
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                    CompanyId = companyId, EmployeeId = employeeId };
                Check(await users.CreateAsync(user, passwords[role]!), "create demo user");
            }
            else
            {
                if (user.CompanyId != companyId || (employeeId.HasValue && user.EmployeeId.HasValue && user.EmployeeId != employeeId))
                    throw new InvalidOperationException("Demo user is already assigned to another company or employee.");
                if (employeeId.HasValue && !user.EmployeeId.HasValue)
                {
                    user.EmployeeId = employeeId;
                    Check(await users.UpdateAsync(user), "link demo employee");
                }
            }
            var existingRoles = await users.GetRolesAsync(user);
            if (existingRoles.Any(existing => existing != role))
                throw new InvalidOperationException("Demo user has an incompatible role. Review the existing account manually.");
            if (!existingRoles.Contains(role)) Check(await users.AddToRoleAsync(user, role), "assign demo role");
        }
    }

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to {action}: {string.Join(", ", result.Errors.Select(error => error.Code))}");
    }
}
