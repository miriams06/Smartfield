namespace SmartField.Integrations.Primavera;

public sealed class PrimaveraEmployeeIntegrationService : IEmployeeIntegrationService
{
    private readonly IPrimaveraClient primaveraClient;

    public PrimaveraEmployeeIntegrationService(IPrimaveraClient primaveraClient)
    {
        this.primaveraClient = primaveraClient;
    }

    public Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken)
    {
        return primaveraClient.GetEmployeesAsync(cancellationToken);
    }

    public Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken)
    {
        return primaveraClient.GetEmployeeAsync(employeeCode, cancellationToken);
    }
}
