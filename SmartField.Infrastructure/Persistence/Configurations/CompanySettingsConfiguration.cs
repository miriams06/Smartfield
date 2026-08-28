using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("CompanySettings");

        builder.HasKey(settings => settings.CompanyId);

        builder.HasOne<Company>()
            .WithOne()
            .HasForeignKey<CompanySettings>(settings => settings.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new CompanySettings
        {
            CompanyId = SmartFieldSeedData.CompanyId,
            RequireGeolocation = false,
            GeofenceMode = GeofenceMode.Disabled,
            AllowBreaks = true,
            AllowProjectSelection = false,
            RequireProjectSelection = false,
            DefaultGeofenceRadiusMeters = 100,
            CreatedAtUtc = SmartFieldSeedData.CreatedAtUtc,
            UpdatedAtUtc = null
        });
    }
}
