using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Authentication;

public static class DevelopmentIdentitySeeder
{
    private const string AdminEmail = "admin@smartfield.local";
    private const string DemoCompanyCode = "SYS-DEMO";
    private const string DemoEmployeeNumber = "FUNC001";

    public static async Task SeedDevelopmentIdentityAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminPassword = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in SmartFieldRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                ThrowIfFailed(roleResult, $"create role '{role}'");
            }
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>();
        var company = await dbContext.Companies
            .IgnoreQueryFilters()
            .SingleAsync(company => company.Code == DemoCompanyCode);
        var employee = await dbContext.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(employee =>
                employee.CompanyId == company.Id
                && employee.EmployeeNumber == DemoEmployeeNumber);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminUser = await userManager.FindByEmailAsync(AdminEmail);

        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                CompanyId = company.Id,
                EmployeeId = employee?.Id,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            ThrowIfFailed(createResult, $"create development admin user '{AdminEmail}'");
        }

        if (!await userManager.IsInRoleAsync(adminUser, SmartFieldRoles.Admin))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(adminUser, SmartFieldRoles.Admin);
            ThrowIfFailed(addToRoleResult, $"assign role '{SmartFieldRoles.Admin}' to '{AdminEmail}'");
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {action}: {errors}");
    }
}
