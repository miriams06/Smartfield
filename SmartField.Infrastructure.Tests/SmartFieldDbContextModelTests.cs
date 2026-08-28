using Microsoft.EntityFrameworkCore;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Tests;

public class SmartFieldDbContextModelTests
{
    [Fact]
    public void DbContext_ExposesExpectedDbSets()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Companies);
        Assert.NotNull(context.CompanySettings);
        Assert.NotNull(context.Employees);
        Assert.NotNull(context.WorkSites);
        Assert.NotNull(context.Projects);
        Assert.NotNull(context.AttendanceEvents);
        Assert.NotNull(context.AttendanceCorrections);
        Assert.NotNull(context.AuditLogs);
        Assert.NotNull(context.ExternalReferences);
        Assert.NotNull(context.IntegrationOutbox);
    }

    [Fact]
    public void Model_ConfiguresRequiredIndexes()
    {
        using var context = CreateContext();
        var model = context.Model;

        AssertHasIndex(model.FindEntityType(typeof(AttendanceEvent))!, false, "CompanyId", "EmployeeId", "ServerTimestampUtc");
        AssertHasIndex(model.FindEntityType(typeof(AttendanceEvent))!, true, "ClientEventId");
        AssertHasIndex(model.FindEntityType(typeof(Employee))!, true, "CompanyId", "EmployeeNumber");
        AssertHasIndex(model.FindEntityType(typeof(WorkSite))!, true, "CompanyId", "Code");
        AssertHasIndex(model.FindEntityType(typeof(Project))!, true, "CompanyId", "Code");
    }

    [Fact]
    public void Model_ConfiguresCompanyQueryFilters()
    {
        using var context = CreateContext();

        var filteredEntityTypes = new[]
        {
            typeof(CompanySettings),
            typeof(Employee),
            typeof(WorkSite),
            typeof(Project),
            typeof(AttendanceEvent),
            typeof(AttendanceCorrection),
            typeof(AuditLog),
            typeof(ExternalReference),
            typeof(IntegrationOutbox)
        };

        Assert.All(filteredEntityTypes, entityType =>
        {
            var modelEntityType = context.Model.FindEntityType(entityType);

            Assert.NotNull(modelEntityType);
            Assert.NotNull(modelEntityType.GetQueryFilter());
        });
    }

    [Fact]
    public void Model_ConfiguresCompanyRelationships()
    {
        using var context = CreateContext();

        var companyScopedEntityTypes = new[]
        {
            typeof(CompanySettings),
            typeof(Employee),
            typeof(WorkSite),
            typeof(Project),
            typeof(AttendanceEvent),
            typeof(AttendanceCorrection),
            typeof(AuditLog),
            typeof(ExternalReference),
            typeof(IntegrationOutbox)
        };

        Assert.All(companyScopedEntityTypes, entityType =>
        {
            var modelEntityType = context.Model.FindEntityType(entityType);

            Assert.NotNull(modelEntityType);
            Assert.Contains(modelEntityType.GetForeignKeys(), foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(Company)
                && foreignKey.Properties.Any(property => property.Name == "CompanyId"));
        });
    }

    [Fact]
    public void Model_ContainsInitialSeedData()
    {
        using var context = CreateContext();

        var companySeed = context.Model.FindEntityType(typeof(Company))!.GetSeedData();
        var employeeSeed = context.Model.FindEntityType(typeof(Employee))!.GetSeedData();

        Assert.Contains(companySeed, seed =>
            seed["Code"]?.ToString() == "SYS-DEMO"
            && seed["Name"]?.ToString() == "SmartField Demo");

        Assert.Contains(employeeSeed, seed =>
            seed["EmployeeNumber"]?.ToString() == "FUNC001"
            && seed["Name"]?.ToString() == "Funcionário Demo");
    }

    private static SmartFieldDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SmartFieldDb_Tests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new SmartFieldDbContext(options);
    }

    private static void AssertHasIndex(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType,
        bool isUnique,
        params string[] propertyNames)
    {
        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique == isUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }
}
