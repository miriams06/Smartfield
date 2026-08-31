using SmartField.Domain.Entities;

namespace SmartField.Application.Employees;

public interface IEmployeeStore
{
    Task<IReadOnlyList<EmployeeDto>> SearchAsync(
        Guid companyId,
        string? search,
        CancellationToken cancellationToken);

    Task<EmployeeDto?> GetAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<Employee?> FindEntityAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<EmployeeOptions> GetOptionsAsync(
        Guid companyId,
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task<bool> EmployeeNumberExistsAsync(
        Guid companyId,
        string employeeNumber,
        Guid? employeeIdToExclude,
        CancellationToken cancellationToken);

    Task<bool> WorkSiteCanBeAssignedAsync(
        Guid companyId,
        Guid workSiteId,
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task<EmployeeUserAssociationStatus> SetUserAssociationAsync(
        Guid companyId,
        Guid employeeId,
        Guid? userId,
        CancellationToken cancellationToken);

    void Add(Employee employee);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
