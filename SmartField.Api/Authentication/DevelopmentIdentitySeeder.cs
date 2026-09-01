using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Authentication;

public static class DevelopmentIdentitySeeder
{
    private const string AdminEmail = "admin@smartfield.local";
    private const string EmployeeEmail = "employee@smartfield.local";

    private const string DemoCompanyCode = "SYS-DEMO";

    private const string DemoAdminEmployeeNumber = "FUNC001";
    private const string DemoMobileEmployeeNumber = "FUNC002";
    private const string DemoMobileEmployeeName = "Funcionario Mobile Demo";

    public static async Task SeedDevelopmentIdentityAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        using var scope = app.Services.CreateScope();

        var configuration =
            scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var adminPassword = configuration["Seed:AdminPassword"];
        var employeePassword = configuration["Seed:EmployeePassword"];

        if (string.IsNullOrWhiteSpace(adminPassword)
            && string.IsNullOrWhiteSpace(employeePassword))
        {
            return;
        }

        var roleManager =
            scope.ServiceProvider.GetRequiredService<
                RoleManager<IdentityRole<Guid>>>();

        foreach (var role in SmartFieldRoles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var roleResult =
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>(role));

            ThrowIfFailed(
                roleResult,
                $"create role '{role}'");
        }

        var dbContext =
            scope.ServiceProvider.GetRequiredService<SmartFieldDbContext>();

        var company = await dbContext.Companies
            .IgnoreQueryFilters()
            .SingleAsync(company =>
                company.Code == DemoCompanyCode);

        var adminEmployee = await dbContext.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(employee =>
                employee.CompanyId == company.Id
                && employee.EmployeeNumber == DemoAdminEmployeeNumber);

        Employee? mobileEmployee = null;

        if (!string.IsNullOrWhiteSpace(employeePassword))
        {
            mobileEmployee = await dbContext.Employees
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(employee =>
                    employee.CompanyId == company.Id
                    && employee.EmployeeNumber == DemoMobileEmployeeNumber);

            if (mobileEmployee is null)
            {
                mobileEmployee = new Employee
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    EmployeeNumber = DemoMobileEmployeeNumber,
                    Name = DemoMobileEmployeeName,
                    Email = EmployeeEmail,
                    IsActive = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };

                dbContext.Employees.Add(mobileEmployee);

                await dbContext.SaveChangesAsync();
            }
        }

        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!string.IsNullOrWhiteSpace(adminPassword))
        {
            await EnsureUserAsync(
                userManager,
                AdminEmail,
                adminPassword,
                company.Id,
                adminEmployee?.Id,
                SmartFieldRoles.Admin);
        }

        if (!string.IsNullOrWhiteSpace(employeePassword))
        {
            if (mobileEmployee is null)
            {
                throw new InvalidOperationException(
                    $"Development employee '{DemoMobileEmployeeNumber}' could not be created.");
            }

            await EnsureUserAsync(
                userManager,
                EmployeeEmail,
                employeePassword,
                company.Id,
                mobileEmployee.Id,
                SmartFieldRoles.Employee);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        Guid companyId,
        Guid? employeeId,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CompanyId = companyId,
                EmployeeId = employeeId,
                IsActive = true
            };

            var createResult =
                await userManager.CreateAsync(user, password);

            ThrowIfFailed(
                createResult,
                $"create development user '{email}'");
        }
        else
        {
            if (user.CompanyId != companyId)
            {
                throw new InvalidOperationException(
                    $"Development user '{email}' belongs to another company.");
            }

            if (employeeId.HasValue
                && user.EmployeeId.HasValue
                && user.EmployeeId.Value != employeeId.Value)
            {
                throw new InvalidOperationException(
                    $"Development user '{email}' is already linked to another employee.");
            }

            if (user.EmployeeId != employeeId
                || !user.IsActive)
            {
                user.EmployeeId = employeeId;
                user.IsActive = true;

                var updateResult =
                    await userManager.UpdateAsync(user);

                ThrowIfFailed(
                    updateResult,
                    $"update development user '{email}'");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var roleResult =
                await userManager.AddToRoleAsync(user, role);

            ThrowIfFailed(
                roleResult,
                $"assign role '{role}' to '{email}'");
        }
    }

    private static void ThrowIfFailed(
        IdentityResult result,
        string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors =
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

        throw new InvalidOperationException(
            $"Failed to {action}: {errors}");
    }
}