using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.Employees;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Employees;

public sealed class EmployeeStore : IEmployeeStore
{
    private readonly SmartFieldDbContext dbContext;

    public EmployeeStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EmployeeDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(employee =>
                employee.EmployeeNumber.Contains(search)
                || employee.Name.Contains(search)
                || (employee.Email != null && employee.Email.Contains(search))
                || (employee.MobilePhone != null && employee.MobilePhone.Contains(search))
                || (employee.ErpEmployeeCode != null && employee.ErpEmployeeCode.Contains(search))
                || dbContext.Users.Any(user =>
                    user.CompanyId == companyId
                    && user.EmployeeId == employee.Id
                    && user.Email != null
                    && user.Email.Contains(search)));
        }

        return await Project(query, companyId)
            .OrderByDescending(employee => employee.IsActive)
            .ThenBy(employee => employee.Name)
            .ThenBy(employee => employee.EmployeeNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<EmployeeDto?> GetAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId
                && employee.Id == employeeId);

        return Project(query, companyId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Employee?> FindEntityAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Employees.SingleOrDefaultAsync(
            employee =>
                employee.CompanyId == companyId
                && employee.Id == employeeId,
            cancellationToken);
    }

    public async Task<EmployeeOptions> GetOptionsAsync(
        Guid companyId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        Guid? currentWorkSiteId = null;

        if (employeeId.HasValue)
        {
            currentWorkSiteId = await dbContext.Employees
                .AsNoTracking()
                .Where(employee =>
                    employee.CompanyId == companyId
                    && employee.Id == employeeId.Value)
                .Select(employee => employee.DefaultWorkSiteId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var workSites = await dbContext.WorkSites
            .AsNoTracking()
            .Where(workSite =>
                workSite.CompanyId == companyId
                && (workSite.IsActive || workSite.Id == currentWorkSiteId))
            .OrderBy(workSite => workSite.Name)
            .ThenBy(workSite => workSite.Code)
            .Select(workSite => new EmployeeWorkSiteOption(
                workSite.Id,
                workSite.Code,
                workSite.Name,
                workSite.IsActive))
            .ToListAsync(cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.CompanyId == companyId
                && ((user.EmployeeId == null && user.IsActive)
                    || (employeeId.HasValue
                        && user.EmployeeId == employeeId.Value)))
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Select(user => new EmployeeUserOption(
                user.Id,
                user.Email ?? user.UserName ?? string.Empty,
                user.IsActive))
            .ToListAsync(cancellationToken);

        return new EmployeeOptions(workSites, users);
    }

    public Task<bool> EmployeeNumberExistsAsync(
        Guid companyId,
        string employeeNumber,
        Guid? employeeIdToExclude,
        CancellationToken cancellationToken)
    {
        return dbContext.Employees.AnyAsync(
            employee =>
                employee.CompanyId == companyId
                && employee.EmployeeNumber == employeeNumber
                && (!employeeIdToExclude.HasValue
                    || employee.Id != employeeIdToExclude.Value),
            cancellationToken);
    }

    public async Task<bool> WorkSiteCanBeAssignedAsync(
        Guid companyId,
        Guid workSiteId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var isCurrentWorkSite = employeeId.HasValue
            && await dbContext.Employees.AnyAsync(
                employee =>
                    employee.CompanyId == companyId
                    && employee.Id == employeeId.Value
                    && employee.DefaultWorkSiteId == workSiteId,
                cancellationToken);

        return await dbContext.WorkSites.AnyAsync(
            workSite =>
                workSite.CompanyId == companyId
                && workSite.Id == workSiteId
                && (workSite.IsActive || isCurrentWorkSite),
            cancellationToken);
    }

    public async Task<EmployeeUserAssociationStatus> SetUserAssociationAsync(
        Guid companyId,
        Guid employeeId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        ApplicationUser? selectedUser = null;

        if (userId.HasValue)
        {
            selectedUser = await dbContext.Users.SingleOrDefaultAsync(
                user =>
                    user.CompanyId == companyId
                    && user.Id == userId.Value,
                cancellationToken);

            if (selectedUser is null
                || (!selectedUser.IsActive && selectedUser.EmployeeId != employeeId))
            {
                return EmployeeUserAssociationStatus.UserNotFound;
            }

            if (selectedUser.EmployeeId.HasValue
                && selectedUser.EmployeeId.Value != employeeId)
            {
                return EmployeeUserAssociationStatus.UserAlreadyAssigned;
            }
        }

        var currentUsers = await dbContext.Users
            .Where(user =>
                user.CompanyId == companyId
                && user.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        foreach (var currentUser in currentUsers)
        {
            currentUser.EmployeeId = null;
        }

        if (selectedUser is not null)
        {
            selectedUser.EmployeeId = employeeId;
        }

        return EmployeeUserAssociationStatus.Success;
    }

    public void Add(Employee employee)
    {
        dbContext.Employees.Add(employee);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
                && sqlException.Message.Contains(
                    "IX_Employees_CompanyId_EmployeeNumber",
                    StringComparison.Ordinal))
        {
            throw new EmployeeNumberConflictException(
                "Já existe um funcionário com este número na empresa.",
                exception);
        }
    }

    private IQueryable<EmployeeDto> Project(
        IQueryable<Employee> employees,
        Guid companyId)
    {
        return employees.Select(employee => new EmployeeDto(
            employee.Id,
            employee.EmployeeNumber,
            employee.Name,
            employee.Email,
            employee.MobilePhone,
            employee.IsActive,
            employee.DefaultWorkSiteId,
            dbContext.WorkSites
                .Where(workSite =>
                    workSite.CompanyId == companyId
                    && workSite.Id == employee.DefaultWorkSiteId)
                .Select(workSite => workSite.Name)
                .FirstOrDefault(),
            dbContext.Users
                .Where(user =>
                    user.CompanyId == companyId
                    && user.EmployeeId == employee.Id)
                .OrderBy(user => user.Id)
                .Select(user => (Guid?)user.Id)
                .FirstOrDefault(),
            dbContext.Users
                .Where(user =>
                    user.CompanyId == companyId
                    && user.EmployeeId == employee.Id)
                .OrderBy(user => user.Id)
                .Select(user => user.Email)
                .FirstOrDefault(),
            employee.ErpEmployeeCode,
            employee.CreatedAtUtc,
            employee.UpdatedAtUtc));
    }
}
