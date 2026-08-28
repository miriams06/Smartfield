using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.EmployeeNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(employee => employee.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(employee => employee.Email)
            .HasMaxLength(320);

        builder.Property(employee => employee.MobilePhone)
            .HasMaxLength(50);

        builder.Property(employee => employee.ExternalSystem)
            .HasMaxLength(100);

        builder.Property(employee => employee.ExternalId)
            .HasMaxLength(100);

        builder.Property(employee => employee.ErpEmployeeCode)
            .HasMaxLength(100);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(employee => employee.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkSite>()
            .WithMany()
            .HasForeignKey(employee => employee.DefaultWorkSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(employee => new { employee.CompanyId, employee.EmployeeNumber })
            .IsUnique();

        builder.HasData(new Employee
        {
            Id = SmartFieldSeedData.EmployeeId,
            CompanyId = SmartFieldSeedData.CompanyId,
            EmployeeNumber = "FUNC001",
            Name = "Funcionário Demo",
            Email = null,
            MobilePhone = null,
            IsActive = true,
            DefaultWorkSiteId = null,
            ExternalSystem = null,
            ExternalId = null,
            ErpEmployeeCode = null,
            CreatedAtUtc = SmartFieldSeedData.CreatedAtUtc,
            UpdatedAtUtc = null
        });
    }
}
