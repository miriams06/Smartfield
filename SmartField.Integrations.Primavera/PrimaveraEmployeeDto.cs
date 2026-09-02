namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraEmployeeDto(
    string EmployeeCode,
    string Name,
    string? Email,
    string? MobilePhone,
    bool IsActive);
