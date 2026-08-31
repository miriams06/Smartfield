using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SmartField.Infrastructure.Persistence;
using SmartField.Infrastructure.Persistence.Migrations;

namespace SmartField.Infrastructure.Tests;

public class SmartFieldMigrationTests
{
    private const string InitialMigrationId = "20260828123000_InitialCreate";

    [Fact]
    public void MigrationAssembly_ContainsInitialMigration()
    {
        using var context = CreateContext();

        Assert.Contains(InitialMigrationId, context.Database.GetMigrations());
    }

    [Fact]
    public void InitialMigration_CreatesExpectedTablesAndIndexes()
    {
        var migration = new InitialCreate();
        var tableNames = migration.UpOperations
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToHashSet(StringComparer.Ordinal);

        var expectedTableNames = new[]
        {
            "AttendanceCorrections",
            "AttendanceEvents",
            "AuditLogs",
            "Companies",
            "CompanySettings",
            "Employees",
            "ExternalReferences",
            "IntegrationOutbox",
            "Projects",
            "WorkSites"
        };

        Assert.Equal(
            expectedTableNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            tableNames.OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var indexes = migration.UpOperations.OfType<CreateIndexOperation>().ToArray();

        AssertIndex(indexes, "AttendanceEvents", true, "ClientEventId");
        AssertIndex(indexes, "AttendanceEvents", false, "CompanyId", "EmployeeId", "ServerTimestampUtc");
        AssertIndex(indexes, "Employees", true, "CompanyId", "EmployeeNumber");
        AssertIndex(indexes, "WorkSites", true, "CompanyId", "Code");
        AssertIndex(indexes, "Projects", true, "CompanyId", "Code");
    }

    [Fact]
    public void InitialMigration_ContainsExpectedDemoSeed()
    {
        var migration = new InitialCreate();
        var seedOperations = migration.UpOperations.OfType<InsertDataOperation>().ToArray();

        AssertSeedValue(seedOperations, "Companies", "Code", "SYS-DEMO");
        AssertSeedValue(seedOperations, "Companies", "Name", "SmartField Demo");
        AssertSeedValue(seedOperations, "CompanySettings", "CompanyId", Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68"));
        AssertSeedValue(seedOperations, "Employees", "EmployeeNumber", "FUNC001");
        AssertSeedValue(seedOperations, "Employees", "Name", "Funcionário Demo");
    }

    [Fact]
    public void InitialMigration_GeneratesSqlServerScriptWithoutOpeningConnection()
    {
        using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: Migration.InitialDatabase,
            toMigration: InitialMigrationId,
            options: MigrationsSqlGenerationOptions.Default);

        Assert.Contains("CREATE TABLE [Companies]", script);
        Assert.Contains("CREATE TABLE [AttendanceEvents]", script);
        Assert.Contains("CREATE UNIQUE INDEX [IX_AttendanceEvents_ClientEventId]", script);
        Assert.Contains("INSERT INTO [Companies]", script);
    }

    private static SmartFieldDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SmartFieldDb_Tests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new SmartFieldDbContext(options);
    }

    private static void AssertIndex(
        IEnumerable<CreateIndexOperation> indexes,
        string table,
        bool isUnique,
        params string[] columns)
    {
        Assert.Contains(indexes, index =>
            index.Table == table
            && index.IsUnique == isUnique
            && index.Columns.SequenceEqual(columns));
    }

    private static void AssertSeedValue(
        IEnumerable<InsertDataOperation> seedOperations,
        string table,
        string column,
        object expectedValue)
    {
        Assert.Contains(seedOperations, operation =>
        {
            if (operation.Table != table)
            {
                return false;
            }

            var columnIndex = Array.IndexOf(operation.Columns, column);
            if (columnIndex < 0)
            {
                return false;
            }

            for (var rowIndex = 0; rowIndex < operation.Values.GetLength(0); rowIndex++)
            {
                if (object.Equals(operation.Values[rowIndex, columnIndex], expectedValue))
                {
                    return true;
                }
            }

            return false;
        });
    }
}
