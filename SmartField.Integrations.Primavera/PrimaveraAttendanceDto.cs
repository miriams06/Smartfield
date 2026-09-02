namespace SmartField.Integrations.Primavera;

public sealed record PrimaveraAttendanceDto(
    Guid AttendanceEventId,
    string EmployeeCode,
    string EventType,
    DateTimeOffset TimestampUtc,
    string? ProjectCode,
    string? WorkSiteCode,
    decimal? Latitude,
    decimal? Longitude);
