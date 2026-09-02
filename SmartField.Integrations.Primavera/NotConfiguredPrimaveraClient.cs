namespace SmartField.Integrations.Primavera;

public sealed class NotConfiguredPrimaveraClient : IPrimaveraClient
{
    private const string NotConfiguredMessage =
        "A integração PRIMAVERA ainda não está configurada.";

    public Task<PrimaveraConnectionResult> TestConnectionAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new PrimaveraConnectionResult(
            IsConfigured: false,
            IsAvailable: false,
            NotConfiguredMessage));
    }

    public Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<PrimaveraEmployeeDto>>([]);
    }

    public Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<PrimaveraEmployeeDto?>(null);
    }

    public Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<PrimaveraProjectDto>>([]);
    }

    public Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<PrimaveraCostCenterDto>>([]);
    }

    public Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
        PrimaveraAttendanceDto attendance,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new PrimaveraAttendanceSendResult(
            IsSuccess: false,
            "NotConfigured",
            NotConfiguredMessage,
            ExternalDocumentId: null));
    }
}
