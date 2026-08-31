using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.CompanyId)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .IsRequired();

        builder.HasIndex(user => new { user.CompanyId, user.NormalizedEmail })
            .IsUnique()
            .HasFilter("[NormalizedEmail] IS NOT NULL");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(user => user.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(user => user.EmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
