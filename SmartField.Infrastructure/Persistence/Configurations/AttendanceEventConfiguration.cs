using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> builder)
    {
        builder.ToTable("AttendanceEvents");

        builder.HasKey(attendanceEvent => attendanceEvent.Id);

        builder.Property(attendanceEvent => attendanceEvent.EventType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attendanceEvent => attendanceEvent.Latitude)
            .HasPrecision(9, 6);

        builder.Property(attendanceEvent => attendanceEvent.Longitude)
            .HasPrecision(9, 6);

        builder.Property(attendanceEvent => attendanceEvent.LocationAccuracyMeters)
            .HasPrecision(10, 2);

        builder.Property(attendanceEvent => attendanceEvent.DistanceFromWorkSiteMeters)
            .HasPrecision(10, 2);

        builder.Property(attendanceEvent => attendanceEvent.Source)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(attendanceEvent => attendanceEvent.Notes)
            .HasMaxLength(1000);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(attendanceEvent => attendanceEvent.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(attendanceEvent => attendanceEvent.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkSite>()
            .WithMany()
            .HasForeignKey(attendanceEvent => attendanceEvent.WorkSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(attendanceEvent => attendanceEvent.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(attendanceEvent => new
        {
            attendanceEvent.CompanyId,
            attendanceEvent.EmployeeId,
            attendanceEvent.ServerTimestampUtc
        });

        builder.HasIndex(attendanceEvent => attendanceEvent.ClientEventId)
            .IsUnique();
    }
}
