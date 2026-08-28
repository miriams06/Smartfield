using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(company => company.Id);

        builder.Property(company => company.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(company => company.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(company => company.Nif)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(company => company.TimeZone)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(company => company.Code)
            .IsUnique();

        builder.HasData(new Company
        {
            Id = SmartFieldSeedData.CompanyId,
            Code = "SYS-DEMO",
            Name = "SmartField Demo",
            Nif = string.Empty,
            TimeZone = "Europe/Lisbon",
            IsActive = true,
            CreatedAtUtc = SmartFieldSeedData.CreatedAtUtc,
            UpdatedAtUtc = null
        });
    }
}
