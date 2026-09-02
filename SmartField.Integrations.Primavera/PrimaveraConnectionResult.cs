namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraConnectionResult(
    bool IsConfigured,
    bool IsAvailable,
    string Message);
