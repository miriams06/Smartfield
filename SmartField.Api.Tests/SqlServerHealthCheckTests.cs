using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartField.Api.HealthChecks;

namespace SmartField.Api.Tests;

public class SqlServerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenSmartFieldDatabaseIsReachable()
    {
        var healthCheck = new SqlServerHealthCheck(CreateConfiguration(
            "Server=.\\SQLEXPRESS;Database=SmartFieldDb;Trusted_Connection=True;TrustServerCertificate=True"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenConnectionStringIsMissing()
    {
        var healthCheck = new SqlServerHealthCheck(CreateConfiguration(null));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static IConfiguration CreateConfiguration(string? connectionString)
    {
        var values = connectionString is null
            ? []
            : new Dictionary<string, string?>
            {
                ["ConnectionStrings:SmartField"] = connectionString
            };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
