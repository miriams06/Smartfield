using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class IntegrationOutboxConfiguration : IEntityTypeConfiguration<IntegrationOutbox>
{
    public void Configure(EntityTypeBuilder<IntegrationOutbox> builder)
    {
        builder.ToTable("IntegrationOutbox");

        builder.HasKey(outbox => outbox.Id);

        builder.Property(outbox => outbox.EventType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(outbox => outbox.EntityType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(outbox => outbox.Payload)
            .IsRequired();

        builder.Property(outbox => outbox.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(IntegrationStatus.Pending)
            .IsRequired();

        builder.Property(outbox => outbox.LastError)
            .HasMaxLength(2000);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(outbox => outbox.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(outbox => new { outbox.CompanyId, outbox.Status, outbox.CreatedAtUtc });
    }
}
