using SmartField.Integrations.Primavera;

namespace SmartField.Integrations.Primavera.Tests;

public class PrimaveraIntegrationServiceTests
{
    [Fact]
    public async Task EmployeeIntegrationService_DelegatesToPrimaveraClient()
    {
        var client = new FakePrimaveraClient();
        var service = new PrimaveraEmployeeIntegrationService(client);

        await service.GetEmployeesAsync(CancellationToken.None);
        await service.GetEmployeeAsync("FUNC001", CancellationToken.None);

        Assert.Equal(1, client.GetEmployeesCalls);
        Assert.Equal("FUNC001", client.LastEmployeeCode);
    }

    [Fact]
    public async Task AttendanceIntegrationService_DelegatesToPrimaveraClient()
    {
        var client = new FakePrimaveraClient();
        var service = new PrimaveraAttendanceIntegrationService(client);
        var attendance = CreateAttendance();

        await service.SendAttendanceAsync(attendance, CancellationToken.None);

        Assert.Equal(attendance, client.LastAttendance);
    }

    [Fact]
    public async Task ProjectIntegrationService_DelegatesToPrimaveraClient()
    {
        var client = new FakePrimaveraClient();
        var service = new PrimaveraProjectIntegrationService(client);

        await service.GetProjectsAsync(CancellationToken.None);
        await service.GetCostCentersAsync(CancellationToken.None);

        Assert.Equal(1, client.GetProjectsCalls);
        Assert.Equal(1, client.GetCostCentersCalls);
    }

    private static PrimaveraAttendanceDto CreateAttendance()
    {
        return new PrimaveraAttendanceDto(
            Guid.NewGuid(),
            "FUNC001",
            "ClockIn",
            new DateTimeOffset(2026, 9, 2, 8, 30, 0, TimeSpan.Zero),
            "OBR-001",
            "SEDE",
            null,
            null);
    }

    private sealed class FakePrimaveraClient : IPrimaveraClient
    {
        public int GetEmployeesCalls { get; private set; }
        public string? LastEmployeeCode { get; private set; }
        public int GetProjectsCalls { get; private set; }
        public int GetCostCentersCalls { get; private set; }
        public PrimaveraAttendanceDto? LastAttendance { get; private set; }

        public Task<PrimaveraConnectionResult> TestConnectionAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PrimaveraConnectionResult(true, true, "OK"));
        }

        public Task<IReadOnlyList<PrimaveraEmployeeDto>> GetEmployeesAsync(
            CancellationToken cancellationToken)
        {
            GetEmployeesCalls++;
            return Task.FromResult<IReadOnlyList<PrimaveraEmployeeDto>>([]);
        }

        public Task<PrimaveraEmployeeDto?> GetEmployeeAsync(
            string employeeCode,
            CancellationToken cancellationToken)
        {
            LastEmployeeCode = employeeCode;
            return Task.FromResult<PrimaveraEmployeeDto?>(null);
        }

        public Task<IReadOnlyList<PrimaveraProjectDto>> GetProjectsAsync(
            CancellationToken cancellationToken)
        {
            GetProjectsCalls++;
            return Task.FromResult<IReadOnlyList<PrimaveraProjectDto>>([]);
        }

        public Task<IReadOnlyList<PrimaveraCostCenterDto>> GetCostCentersAsync(
            CancellationToken cancellationToken)
        {
            GetCostCentersCalls++;
            return Task.FromResult<IReadOnlyList<PrimaveraCostCenterDto>>([]);
        }

        public Task<PrimaveraAttendanceSendResult> SendAttendanceAsync(
            PrimaveraAttendanceDto attendance,
            CancellationToken cancellationToken)
        {
            LastAttendance = attendance;
            return Task.FromResult(new PrimaveraAttendanceSendResult(
                true,
                "Sent",
                "OK",
                "DOC001"));
        }
    }
}
