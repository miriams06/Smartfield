using Microsoft.EntityFrameworkCore;
using SmartField.Domain.Entities;

namespace SmartField.Infrastructure.Persistence;

public class SmartFieldDbContext : DbContext
{
    public SmartFieldDbContext(DbContextOptions<SmartFieldDbContext> options)
        : base(options)
    {
    }

    public Guid? CurrentCompanyId { get; set; }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<WorkSite> WorkSites => Set<WorkSite>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();

    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ExternalReference> ExternalReferences => Set<ExternalReference>();

    public DbSet<IntegrationOutbox> IntegrationOutbox => Set<IntegrationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartFieldDbContext).Assembly);
        ConfigureCompanyFilters(modelBuilder);
    }

    private void ConfigureCompanyFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanySettings>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<Employee>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<WorkSite>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<Project>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<AttendanceEvent>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<AttendanceCorrection>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<ExternalReference>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);

        modelBuilder.Entity<IntegrationOutbox>()
            .HasQueryFilter(entity => CurrentCompanyId.HasValue && entity.CompanyId == CurrentCompanyId.Value);
    }
}
