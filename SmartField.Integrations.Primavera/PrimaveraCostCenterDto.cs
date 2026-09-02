namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraCostCenterDto(
    string CostCenterCode,
    string Name,
    bool IsActive);
