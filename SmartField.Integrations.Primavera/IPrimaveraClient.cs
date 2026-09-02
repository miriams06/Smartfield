namespace SmartField.Integrations.Primavera;

public interface IPrimaveraClient
{
    Task<PrimaveraConnectionResult> TestConnectionAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken);

    Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
        CancellationToken cancellationToken);

    Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
        PrimaveraAttendanceDto attendance,
        CancellationToken cancellationToken);
}
