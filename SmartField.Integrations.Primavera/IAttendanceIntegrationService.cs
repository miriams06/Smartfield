namespace SmartField.Integrations.Primavera;

public interface IAttendanceIntegrationService
{
    Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
        PrimaveraAttendanceDto attendance,
        CancellationToken cancellationToken);
}
