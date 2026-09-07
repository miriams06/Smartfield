using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Tests;

// Opt in with a SQL Server connection that permits creating a disposable database.
public sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTFIELD_TEST_SQLSERVER")))
        {
            Skip = "Set SMARTFIELD_TEST_SQLSERVER to run SQL Server persistence tests.";
        }
    }
}

public class ProjectPersistenceTests
{
    private const string PreviousMigration = "20260828160138_AddIdentity";
    private static readonly Guid CompanyId = Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");

    [SqlServerTheory]
    [InlineData(ProjectType.Construction, ProjectStatus.Draft)]
    [InlineData(ProjectType.Maintenance, ProjectStatus.Active)]
    [InlineData(ProjectType.Intervention, ProjectStatus.Closed)]
    [InlineData(ProjectType.Internal, ProjectStatus.Cancelled)]
    [InlineData(ProjectType.Other, ProjectStatus.Draft)]
    public async Task SaveChanges_PreservesExplicitEnumsAcrossMigrationAndRollback(
        ProjectType projectType, ProjectStatus status)
    {
        var connection = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("SMARTFIELD_TEST_SQLSERVER"))
        {
            InitialCatalog = $"SmartField_ProjectEnums_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .Options;
        await using var context = new SmartFieldDbContext(options) { CurrentCompanyId = CompanyId };

        try
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            Assert.Equal(2, await CountDefaultsAsync(context));
            await migrator.MigrateAsync();
            Assert.Equal(0, await CountDefaultsAsync(context));

            var project = new Project
            {
                CompanyId = CompanyId,
                Code = "ENUM-TEST",
                Name = "Project enum regression test",
                ProjectType = projectType,
                Status = status,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            context.Projects.Add(project);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var stored = await context.Projects.SingleAsync(item => item.Id == project.Id);
            Assert.Equal(projectType, stored.ProjectType);
            Assert.Equal(status, stored.Status);
            var storedType = await context.Database.SqlQuery<string>(
                $"SELECT [ProjectType] AS [Value] FROM [Projects] WHERE [Id] = {project.Id}").SingleAsync();
            var storedStatus = await context.Database.SqlQuery<string>(
                $"SELECT [Status] AS [Value] FROM [Projects] WHERE [Id] = {project.Id}").SingleAsync();
            Assert.Equal(projectType.ToString(), storedType);
            Assert.Equal(status.ToString(), storedStatus);

            await migrator.MigrateAsync(PreviousMigration);
            Assert.Equal(2, await CountDefaultsAsync(context));
            await migrator.MigrateAsync();
            Assert.Equal(0, await CountDefaultsAsync(context));
            context.ChangeTracker.Clear();
            stored = await context.Projects.SingleAsync(item => item.Id == project.Id);
            Assert.Equal(projectType, stored.ProjectType);
            Assert.Equal(status, stored.Status);
        }
        finally
        {
            // Only the uniquely named database created by this test is removed.
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static Task<int> CountDefaultsAsync(SmartFieldDbContext context) =>
        context.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value]
            FROM sys.default_constraints AS d
            INNER JOIN sys.columns AS c
                ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
            WHERE d.parent_object_id = OBJECT_ID(N'[dbo].[Projects]')
                AND c.name IN (N'ProjectType', N'Status')
            """).SingleAsync();
}
