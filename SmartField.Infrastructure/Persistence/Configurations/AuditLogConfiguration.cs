using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.EntityType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(auditLog => auditLog.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(auditLog => auditLog.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(auditLog => new { auditLog.CompanyId, auditLog.EntityType, auditLog.EntityId });
    }
}
