namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraAttendanceSendResult(
    bool IsSuccess,
    string Status,
    string Message,
    string? ExternalDocumentId);
