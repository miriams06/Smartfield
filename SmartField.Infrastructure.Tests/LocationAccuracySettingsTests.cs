using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Geolocation;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Tests;

public class LocationAccuracySettingsTests
{
    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task Migration_DefaultsExistingCompanies_AndAccuracyRemainsCompanyScoped()
    {
        var connection = new SqlConnectionStringBuilder(SqlServerIntegrationTestConfiguration.ConnectionString)
        { InitialCatalog = $"SmartField_LocationAccuracy_{Guid.NewGuid():N}" };
        await using var db = new SmartFieldDbContext(new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260908150631_AddDailyWorkReports");
            var company = await db.Companies.SingleAsync();
            var other = new Company { Code = "OTHER-GPS", Name = "Other GPS" };
            db.Companies.Add(other);
            await db.SaveChangesAsync();
            // Insert using the old schema to verify the backfill for a non-seeded company.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CompanySettings (CompanyId, RequireGeolocation, GeofenceMode, AllowBreaks, AllowProjectSelection, RequireProjectSelection, DefaultGeofenceRadiusMeters, CreatedAtUtc) VALUES ({other.Id}, 0, 0, 1, 0, 0, 100, {DateTimeOffset.UtcNow})");
            await migrator.MigrateAsync();
            Assert.All(await db.CompanySettings.IgnoreQueryFilters().ToListAsync(), x => Assert.Equal(100, x.MaximumLocationAccuracyMeters));
            db.CurrentCompanyId = company.Id;
            var ownSettings = await db.CompanySettings.SingleAsync();
            ownSettings.MaximumLocationAccuracyMeters = 25;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var store = new GeolocationStore(db);
            Assert.Equal(25, (await store.GetValidationReferenceAsync(company.Id, null, default))!.MaximumLocationAccuracyMeters);
            Assert.Empty(await db.CompanySettings.Where(x => x.CompanyId == other.Id).ToListAsync());
            db.CurrentCompanyId = other.Id;
            Assert.Equal(100, (await store.GetValidationReferenceAsync(other.Id, null, default))!.MaximumLocationAccuracyMeters);
            Assert.Equal(100, (await db.CompanySettings.SingleAsync()).MaximumLocationAccuracyMeters);
            await migrator.MigrateAsync("20260908150631_AddDailyWorkReports");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.All(await db.CompanySettings.IgnoreQueryFilters().ToListAsync(), x => Assert.Equal(100, x.MaximumLocationAccuracyMeters));
        }
        finally
        {
            // Only this test's uniquely named database is removed.
            await db.Database.EnsureDeletedAsync();
        }
    }
}
