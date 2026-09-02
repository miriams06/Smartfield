namespace SmartField.Integrations.Primavera;

public sealed class PrimaveraProjectIntegrationService : IProjectIntegrationService
{
    private readonly IPrimaveraClient primaveraClient;

    public PrimaveraProjectIntegrationService(IPrimaveraClient primaveraClient)
    {
        this.primaveraClient = primaveraClient;
    }

    public Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        return primaveraClient.GetProjectsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
        CancellationToken cancellationToken)
    {
        return primaveraClient.GetCostCentersAsync(cancellationToken);
    }
}
