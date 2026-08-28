using SmartField.Domain.Enums;

namespace SmartField.Domain.Entities;

public class AttendanceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid EmployeeId { get; set; }

    public AttendanceEventType EventType { get; set; }

    public DateTimeOffset ServerTimestampUtc { get; set; }

    public DateTimeOffset? ClientTimestampUtc { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? LocationAccuracyMeters { get; set; }

    public Guid? WorkSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public bool? IsInsideGeofence { get; set; }

    public decimal? DistanceFromWorkSiteMeters { get; set; }

    public AttendanceSource Source { get; set; } = AttendanceSource.PWA;

    public Guid ClientEventId { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
