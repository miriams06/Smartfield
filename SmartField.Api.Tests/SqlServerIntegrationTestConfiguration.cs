namespace SmartField.Api.Tests;

internal static class SqlServerIntegrationTestConfiguration
{
    public const string EnvironmentVariableName = "SMARTFIELD_TEST_CONNECTION_STRING";
    public const string MissingConnectionStringMessage =
        "SQL Server integration test skipped. Set SMARTFIELD_TEST_CONNECTION_STRING to a reachable test database.";

    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariableName);
}

internal sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(SqlServerIntegrationTestConfiguration.ConnectionString))
        {
            Skip = SqlServerIntegrationTestConfiguration.MissingConnectionStringMessage;
        }
    }
}
