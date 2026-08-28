using SmartField.Domain.Enums;

namespace SmartField.Domain.Entities;

public class AttendanceCorrection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid AttendanceEventId { get; set; }

    public DateTimeOffset OriginalTimestampUtc { get; set; }

    public DateTimeOffset CorrectedTimestampUtc { get; set; }

    public AttendanceEventType OriginalEventType { get; set; }

    public AttendanceEventType CorrectedEventType { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid CorrectedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
