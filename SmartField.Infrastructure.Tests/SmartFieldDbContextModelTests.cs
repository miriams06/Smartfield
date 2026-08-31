using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Identity;
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

        AssertHasIndex(model.FindEntityType(typeof(ApplicationUser))!, true, "CompanyId", "NormalizedEmail");
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
    public void Model_ConfiguresApplicationUserRelationships()
    {
        using var context = CreateContext();

        var applicationUser = context.Model.FindEntityType(typeof(ApplicationUser));

        Assert.NotNull(applicationUser);
        Assert.Contains(applicationUser.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Company)
            && foreignKey.Properties.Any(property => property.Name == "CompanyId"));
        Assert.Contains(applicationUser.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Employee)
            && foreignKey.Properties.Any(property => property.Name == "EmployeeId"));
    }

    [Fact]
    public void Model_ContainsInitialSeedData()
    {
        using var context = CreateContext();

        var designTimeModel = context
            .GetService<IDesignTimeModel>()
            .Model;

        var companySeed = designTimeModel
            .FindEntityType(typeof(Company))!
            .GetSeedData();

        var employeeSeed = designTimeModel
            .FindEntityType(typeof(Employee))!
            .GetSeedData();

        Assert.Contains(companySeed, seed =>
            seed["Code"]?.ToString() == "SYS-DEMO"
            && seed["Name"]?.ToString() == "SmartField Demo");

        Assert.Contains(employeeSeed, seed =>
            seed["EmployeeNumber"]?.ToString() == "FUNC001"
            && seed["Name"]?.ToString() == "Funcionário Demo");
    }

    [Fact]
    public void Model_ContainsInitialIdentityRoles()
    {
        using var context = CreateContext();

        var designTimeModel = context
            .GetService<IDesignTimeModel>()
            .Model;

        var roleSeed = designTimeModel
            .FindEntityType(typeof(IdentityRole<Guid>))!
            .GetSeedData();

        Assert.Contains(roleSeed, seed => seed["Name"]?.ToString() == SmartFieldRoles.Admin);
        Assert.Contains(roleSeed, seed => seed["Name"]?.ToString() == SmartFieldRoles.Manager);
        Assert.Contains(roleSeed, seed => seed["Name"]?.ToString() == SmartFieldRoles.Employee);
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
