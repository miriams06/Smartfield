using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class WorkSiteConfiguration : IEntityTypeConfiguration<WorkSite>
{
    public void Configure(EntityTypeBuilder<WorkSite> builder)
    {
        builder.ToTable("WorkSites");

        builder.HasKey(workSite => workSite.Id);

        builder.Property(workSite => workSite.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(workSite => workSite.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(workSite => workSite.Address)
            .HasMaxLength(500);

        builder.Property(workSite => workSite.Latitude)
            .HasPrecision(9, 6);

        builder.Property(workSite => workSite.Longitude)
            .HasPrecision(9, 6);

        builder.Property(workSite => workSite.ExternalSystem)
            .HasMaxLength(100);

        builder.Property(workSite => workSite.ExternalId)
            .HasMaxLength(100);

        builder.Property(workSite => workSite.ErpCostCenterCode)
            .HasMaxLength(100);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(workSite => workSite.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(workSite => new { workSite.CompanyId, workSite.Code })
            .IsUnique();
    }
}
