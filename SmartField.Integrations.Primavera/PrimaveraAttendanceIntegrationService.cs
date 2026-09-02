namespace SmartField.Integrations.Primavera;

public sealed class PrimaveraAttendanceIntegrationService : IAttendanceIntegrationService
{
    private readonly IPrimaveraClient primaveraClient;

    public PrimaveraAttendanceIntegrationService(IPrimaveraClient primaveraClient)
    {
        this.primaveraClient = primaveraClient;
    }

    public Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
        PrimaveraAttendanceDto attendance,
        CancellationToken cancellationToken)
    {
        return primaveraClient.SendAttendanceAsync(attendance, cancellationToken);
    }
}
