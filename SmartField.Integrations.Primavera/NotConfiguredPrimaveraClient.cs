using Microsoft.Extensions.Logging;

namespace SmartField.Integrations.Primavera;

public sealed class NotConfiguredPrimaveraClient : IPrimaveraClient
{
    private const string NotConfiguredMessage =
        "A integração PRIMAVERA ainda não está configurada.";

    private readonly ILogger<NotConfiguredPrimaveraClient>? logger;

    public NotConfiguredPrimaveraClient(
        ILogger<NotConfiguredPrimaveraClient>? logger = null)
    {
        this.logger = logger;
    }

    public Task<PrimaveraConnectionResult> TestConnectionAsync(
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(TestConnectionAsync));

        return Task.FromResult(new PrimaveraConnectionResult(
            IsConfigured: false,
            IsAvailable: false,
            NotConfiguredMessage));
    }

    public Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(GetEmployeesAsync));

        return Task.FromResult<IReadOnlyList<PrimaveraEmployeeDto>>([]);
    }

    public Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
        string employeeCode,
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(GetEmployeeAsync));

        return Task.FromResult<PrimaveraEmployeeDto?>(null);
    }

    public Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(GetProjectsAsync));

        return Task.FromResult<IReadOnlyList<PrimaveraProjectDto>>([]);
    }

    public Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(GetCostCentersAsync));

        return Task.FromResult<IReadOnlyList<PrimaveraCostCenterDto>>([]);
    }

    public Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
        PrimaveraAttendanceDto attendance,
        CancellationToken cancellationToken)
    {
        LogIntegrationAttempt(nameof(SendAttendanceAsync));

        return Task.FromResult(new PrimaveraAttendanceSendResult(
            IsSuccess: false,
            "NotConfigured",
            NotConfiguredMessage,
            ExternalDocumentId: null));
    }

    private void LogIntegrationAttempt(string operation)
    {
        logger?.LogInformation(
            "PRIMAVERA integration attempt {Operation} skipped because the integration is not configured.",
            operation);
    }
}
