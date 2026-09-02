namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraProjectDto(
    string ProjectCode,
    string Name,
    string? CustomerName,
    string? CostCenterCode,
    bool IsActive);
