using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class DailyWorkReportConfiguration : IEntityTypeConfiguration<DailyWorkReport>
{
    public void Configure(EntityTypeBuilder<DailyWorkReport> builder)
    {
        builder.ToTable("DailyWorkReports");
        builder.HasKey(report => report.Id);
        builder.Property(report => report.WorkDate).HasColumnType("date");
        builder.Property(report => report.Summary).HasMaxLength(4000).IsRequired();
        builder.HasIndex(report => new { report.CompanyId, report.EmployeeId, report.WorkDate }).IsUnique();
        builder.HasIndex(report => report.ClockOutAttendanceEventId).IsUnique();
        builder.HasOne<Company>().WithMany().HasForeignKey(report => report.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(report => report.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttendanceEvent>().WithMany().HasForeignKey(report => report.ClockOutAttendanceEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
