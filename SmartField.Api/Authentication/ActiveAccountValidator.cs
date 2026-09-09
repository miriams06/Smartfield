using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Api.Authentication;

public sealed class ActiveAccountValidator(SmartFieldDbContext dbContext) : JwtBearerEvents
{
    public Task<bool> IsActiveAsync(Guid userId, Guid companyId, Guid? employeeId,
        CancellationToken cancellationToken)
    {
        // Authentication runs before the current-company provider has an authenticated user.
        // Scope the employee explicitly instead of relying on its global query filter.
        return dbContext.Users.AsNoTracking().AnyAsync(user =>
            user.Id == userId && user.CompanyId == companyId
            && user.EmployeeId == employeeId && user.IsActive
            && (user.EmployeeId == null || dbContext.Employees.IgnoreQueryFilters().Any(employee =>
                employee.Id == user.EmployeeId && employee.CompanyId == user.CompanyId
                && employee.IsActive)), cancellationToken);
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var employeeClaim = principal?.FindFirstValue(SmartFieldClaimTypes.EmployeeId);
        Guid? employeeId = null;
        if (employeeClaim is not null)
        {
            if (!Guid.TryParse(employeeClaim, out var parsedEmployeeId))
            {
                context.Fail("Invalid account.");
                return;
            }
            employeeId = parsedEmployeeId;
        }

        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !Guid.TryParse(principal?.FindFirstValue(SmartFieldClaimTypes.CompanyId), out var companyId)
            || !await IsActiveAsync(userId, companyId, employeeId, context.HttpContext.RequestAborted))
        {
            context.Fail("Account is no longer available.");
        }
    }
}
