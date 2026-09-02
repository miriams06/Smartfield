namespace SmartField.Integrations.Primavera;

public interface IEmployeeIntegrationService
{
    Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken);

    Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken);
}
