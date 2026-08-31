using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SmartField.Application.Abstractions;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;

namespace SmartField.Infrastructure.Persistence;

public class SmartFieldDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentCompanyProvider? currentCompanyProvider;

    public SmartFieldDbContext(
        DbContextOptions<SmartFieldDbContext> options,
        ICurrentCompanyProvider? currentCompanyProvider = null)
        : base(options)
    {
        this.currentCompanyProvider = currentCompanyProvider;
    }

    public Guid? CurrentCompanyId { get; set; }

    private Guid? CompanyFilterId => CurrentCompanyId ?? currentCompanyProvider?.CompanyId;

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
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<Employee>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<WorkSite>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<Project>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<AttendanceEvent>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<AttendanceCorrection>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<ExternalReference>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);

        modelBuilder.Entity<IntegrationOutbox>()
            .HasQueryFilter(entity => CompanyFilterId.HasValue && entity.CompanyId == CompanyFilterId.Value);
    }
}
