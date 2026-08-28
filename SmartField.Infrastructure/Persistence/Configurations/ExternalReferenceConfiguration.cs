using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class ExternalReferenceConfiguration : IEntityTypeConfiguration<ExternalReference>
{
    public void Configure(EntityTypeBuilder<ExternalReference> builder)
    {
        builder.ToTable("ExternalReferences");

        builder.HasKey(externalReference => externalReference.Id);

        builder.Property(externalReference => externalReference.SystemName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(externalReference => externalReference.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(externalReference => externalReference.ExternalEntityId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(externalReference => externalReference.ExternalCode)
            .HasMaxLength(100);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(externalReference => externalReference.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(externalReference => new
        {
            externalReference.CompanyId,
            externalReference.SystemName,
            externalReference.EntityType,
            externalReference.LocalEntityId
        });
    }
}
