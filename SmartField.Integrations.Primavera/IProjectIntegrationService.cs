namespace SmartField.Integrations.Primavera;

public interface IProjectIntegrationService
{
    Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
        CancellationToken cancellationToken);
}
