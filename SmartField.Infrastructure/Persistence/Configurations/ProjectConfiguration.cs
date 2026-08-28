using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.ProjectType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ProjectType.Other)
            .IsRequired();

        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ProjectStatus.Draft)
            .IsRequired();

        builder.Property(project => project.CustomerName)
            .HasMaxLength(200);

        builder.Property(project => project.ExternalSystem)
            .HasMaxLength(100);

        builder.Property(project => project.ExternalId)
            .HasMaxLength(100);

        builder.Property(project => project.ErpProjectCode)
            .HasMaxLength(100);

        builder.Property(project => project.ErpCostCenterCode)
            .HasMaxLength(100);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(project => project.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkSite>()
            .WithMany()
            .HasForeignKey(project => project.WorkSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(project => new { project.CompanyId, project.Code })
            .IsUnique();
    }
}
