using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> builder)
    {
        builder.ToTable("AttendanceCorrections");

        builder.HasKey(correction => correction.Id);

        builder.Property(correction => correction.OriginalEventType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(correction => correction.CorrectedEventType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(correction => correction.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(correction => correction.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AttendanceEvent>()
            .WithMany()
            .HasForeignKey(correction => correction.AttendanceEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
