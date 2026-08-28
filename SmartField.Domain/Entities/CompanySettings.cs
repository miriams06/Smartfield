using SmartField.Domain.Enums;

namespace SmartField.Domain.Entities;

public class CompanySettings
{
    public Guid CompanyId { get; set; }

    public bool RequireGeolocation { get; set; }

    public GeofenceMode GeofenceMode { get; set; } = GeofenceMode.Disabled;

    public bool AllowBreaks { get; set; } = true;

    public bool AllowProjectSelection { get; set; }

    public bool RequireProjectSelection { get; set; }

    public int DefaultGeofenceRadiusMeters { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
