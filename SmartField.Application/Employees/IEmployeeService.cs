namespace SmartField.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeeResult<IReadOnlyList<EmployeeDto>>> SearchAsync(
        string? search,
        CancellationToken cancellationToken);

    Task<EmployeeResult<EmployeeDto>> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<EmployeeResult<EmployeeOptions>> GetOptionsAsync(
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task<EmployeeResult<EmployeeDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeResult<EmployeeDto>> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken);
}
