namespace SmartField.Infrastructure.Tests;

internal static class SqlServerIntegrationTestConfiguration
{
    public const string EnvironmentVariableName = "SMARTFIELD_TEST_CONNECTION_STRING";
    public const string MissingConnectionStringMessage =
        "SQL Server integration test skipped. Set SMARTFIELD_TEST_CONNECTION_STRING to a SQL Server instance that permits disposable test databases.";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariableName)!;
}

internal sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                SqlServerIntegrationTestConfiguration.EnvironmentVariableName)))
        {
            Skip = SqlServerIntegrationTestConfiguration.MissingConnectionStringMessage;
        }
    }
}

internal sealed class SqlServerIntegrationTheoryAttribute : TheoryAttribute
{
    public SqlServerIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                SqlServerIntegrationTestConfiguration.EnvironmentVariableName)))
        {
            Skip = SqlServerIntegrationTestConfiguration.MissingConnectionStringMessage;
        }
    }
}
