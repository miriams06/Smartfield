namespace SmartField.Domain.Entities;

public class DailyWorkReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Guid ClockOutAttendanceEventId { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
