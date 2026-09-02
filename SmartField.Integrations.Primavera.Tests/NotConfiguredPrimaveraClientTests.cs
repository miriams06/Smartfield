using SmartField.Integrations.Primavera;

namespace SmartField.Integrations.Primavera.Tests;

public class NotConfiguredPrimaveraClientTests
{
    [Fact]
    public async Task TestConnectionAsync_ReturnsNotConfigured()
    {
        var client = new NotConfiguredPrimaveraClient();

        var result = await client.TestConnectionAsync(CancellationToken.None);

        Assert.False(result.IsConfigured);
        Assert.False(result.IsAvailable);
        Assert.Contains("PRIMAVERA", result.Message);
    }

    [Fact]
    public async Task ReadMethods_ReturnEmptyResultsWithoutCallingErp()
    {
        var client = new NotConfiguredPrimaveraClient();

        var employees = await client.GetEmployeesAsync(CancellationToken.None);
        var employee = await client.GetEmployeeAsync("FUNC001", CancellationToken.None);
        var projects = await client.GetProjectsAsync(CancellationToken.None);
        var costCenters = await client.GetCostCentersAsync(CancellationToken.None);

        Assert.Empty(employees);
        Assert.Null(employee);
        Assert.Empty(projects);
        Assert.Empty(costCenters);
    }

    [Fact]
    public async Task SendAttendanceAsync_ReturnsNotConfiguredFailure()
    {
        var client = new NotConfiguredPrimaveraClient();

        var result = await client.SendAttendanceAsync(
            new PrimaveraAttendanceDto(
                Guid.NewGuid(),
                "FUNC001",
                "ClockIn",
                new DateTimeOffset(2026, 9, 2, 8, 30, 0, TimeSpan.Zero),
                "OBR-001",
                "SEDE",
                38.722252m,
                -9.139337m),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("NotConfigured", result.Status);
        Assert.Null(result.ExternalDocumentId);
    }
}
