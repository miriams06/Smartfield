using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.Abstractions;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Attendance;
using SmartField.Infrastructure.Outbox;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTFIELD_TEST_SQLSERVER")))
        {
            Skip = "Set SMARTFIELD_TEST_SQLSERVER to run SQL Server concurrency tests.";
        }
    }
}

public class AttendanceConcurrencyTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid UserId =
        Guid.Parse("4a290c06-2a4b-4f22-a2df-76111c8d055b");
    private static readonly DateTimeOffset ServerNow =
        new(2026, 9, 7, 13, 30, 0, TimeSpan.Zero);

    [SqlServerFact]
    public async Task ConcurrentClockIns_WithDifferentClientEventIds_PersistOnlyOne()
    {
        var connectionString = CreateConnectionString();

        try
        {
            var employeeId = await InitializeDatabaseAsync(connectionString);

            await using (var firstContext = CreateContext(connectionString))
            await using (var secondContext = CreateContext(connectionString))
            {
                var firstService = CreateService(firstContext, employeeId);
                var secondService = CreateService(secondContext, employeeId);
                var firstRequest = CreateRequest("ClockIn", Guid.NewGuid());
                var secondRequest = CreateRequest("ClockIn", Guid.NewGuid());

                var results = await RunConcurrentlyAsync(
                    () => firstService.PunchAsync(firstRequest, CancellationToken.None),
                    () => secondService.PunchAsync(secondRequest, CancellationToken.None));

                Assert.Single(results.Where(result => result.IsSuccess));
                Assert.Single(results.Where(result => result.Error == AttendanceError.InvalidSequence));
            }

            await using var assertionContext = CreateContext(connectionString);
            Assert.Equal(
                1,
                await assertionContext.AttendanceEvents.CountAsync(
                    item => item.EmployeeId == employeeId
                        && item.EventType == AttendanceEventType.ClockIn));
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ConcurrentClockOuts_WithDifferentClientEventIds_PersistOnlyOne()
    {
        var connectionString = CreateConnectionString();

        try
        {
            var employeeId = await InitializeDatabaseAsync(connectionString);
            await SeedClockInAsync(connectionString, employeeId);

            await using (var firstContext = CreateContext(connectionString))
            await using (var secondContext = CreateContext(connectionString))
            {
                var firstService = CreateService(firstContext, employeeId);
                var secondService = CreateService(secondContext, employeeId);
                var firstRequest = CreateRequest("ClockOut", Guid.NewGuid());
                var secondRequest = CreateRequest("ClockOut", Guid.NewGuid());

                var results = await RunConcurrentlyAsync(
                    () => firstService.PunchAsync(firstRequest, CancellationToken.None),
                    () => secondService.PunchAsync(secondRequest, CancellationToken.None));

                Assert.Single(results.Where(result => result.IsSuccess));
                Assert.Single(results.Where(result => result.Error == AttendanceError.InvalidSequence));
            }

            await using var assertionContext = CreateContext(connectionString);
            Assert.Equal(
                1,
                await assertionContext.AttendanceEvents.CountAsync(
                    item => item.EmployeeId == employeeId
                        && item.EventType == AttendanceEventType.ClockOut));
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [SqlServerFact]
    public async Task ConcurrentClockIns_WithSameClientEventId_RemainIdempotent()
    {
        var connectionString = CreateConnectionString();

        try
        {
            var employeeId = await InitializeDatabaseAsync(connectionString);
            var clientEventId = Guid.NewGuid();

            await using (var firstContext = CreateContext(connectionString))
            await using (var secondContext = CreateContext(connectionString))
            {
                var firstService = CreateService(firstContext, employeeId);
                var secondService = CreateService(secondContext, employeeId);
                var firstRequest = CreateRequest("ClockIn", clientEventId);
                var secondRequest = CreateRequest("ClockIn", clientEventId);

                var results = await RunConcurrentlyAsync(
                    () => firstService.PunchAsync(firstRequest, CancellationToken.None),
                    () => secondService.PunchAsync(secondRequest, CancellationToken.None));

                Assert.All(results, result => Assert.True(result.IsSuccess));
                Assert.Single(results.Where(result => result.Value?.IsDuplicate == false));
                Assert.Single(results.Where(result => result.Value?.IsDuplicate == true));
            }

            await using var assertionContext = CreateContext(connectionString);
            Assert.Equal(
                1,
                await assertionContext.AttendanceEvents.CountAsync(
                    item => item.EmployeeId == employeeId
                        && item.ClientEventId == clientEventId));
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task<Guid> InitializeDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        return await context.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == CompanyId
                && employee.EmployeeNumber == "FUNC001")
            .Select(employee => employee.Id)
            .SingleAsync();
    }

    private static async Task SeedClockInAsync(
        string connectionString,
        Guid employeeId)
    {
        await using var context = CreateContext(connectionString);
        context.AttendanceEvents.Add(new AttendanceEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = CompanyId,
            EmployeeId = employeeId,
            EventType = AttendanceEventType.ClockIn,
            ServerTimestampUtc = ServerNow.AddHours(-9),
            ClientEventId = Guid.NewGuid(),
            Source = AttendanceSource.PWA,
            CreatedAtUtc = ServerNow.AddHours(-9)
        });
        await context.SaveChangesAsync();
    }

    private static IAttendanceService CreateService(
        SmartFieldDbContext context,
        Guid employeeId)
    {
        var companyProvider = new FakeCurrentCompanyProvider();
        var userProvider = new FakeCurrentUserProvider(employeeId);
        var store = new AttendanceStore(context);
        var innerService = new AttendanceService(
            store,
            companyProvider,
            userProvider,
            new AcceptingGeolocationService(),
            new IntegrationOutboxService(new IntegrationOutboxStore(context)),
            new FixedTimeProvider());

        return new SerializedAttendanceService(
            innerService,
            new SqlServerAttendancePunchConcurrencyGate(context),
            companyProvider,
            userProvider);
    }

    private static AttendancePunchRequest CreateRequest(
        string eventType,
        Guid clientEventId)
    {
        return new AttendancePunchRequest(
            eventType,
            clientEventId,
            ServerNow,
            null,
            null,
            null,
            null,
            null);
    }

    private static async Task<AttendanceResult<AttendancePunchDto>[]> RunConcurrentlyAsync(
        Func<Task<AttendanceResult<AttendancePunchDto>>> first,
        Func<Task<AttendanceResult<AttendancePunchDto>>> second)
    {
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<AttendanceResult<AttendancePunchDto>> RunAsync(
            Func<Task<AttendanceResult<AttendancePunchDto>>> operation)
        {
            await start.Task;
            return await operation();
        }

        var firstTask = RunAsync(first);
        var secondTask = RunAsync(second);
        start.SetResult(true);

        return await Task.WhenAll(firstTask, secondTask);
    }

    private static SmartFieldDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SmartFieldDbContext(options)
        {
            CurrentCompanyId = CompanyId
        };
    }

    private static string CreateConnectionString()
    {
        var connection = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("SMARTFIELD_TEST_SQLSERVER"))
        {
            InitialCatalog = $"SmartField_AttendanceConcurrency_{Guid.NewGuid():N}"
        };

        return connection.ConnectionString;
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public Guid? CompanyId => AttendanceConcurrencyTests.CompanyId;
    }

    private sealed class FakeCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid employeeId;

        public FakeCurrentUserProvider(Guid employeeId)
        {
            this.employeeId = employeeId;
        }

        public Guid? UserId => AttendanceConcurrencyTests.UserId;
        public Guid? EmployeeId => employeeId;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ServerNow;
    }

    private sealed class AcceptingGeolocationService : IGeolocationService
    {
        public Task<GeolocationResult<GeolocationValidationDto>> ValidateAsync(
            GeolocationValidationRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                GeolocationResult<GeolocationValidationDto>.Success(
                    new GeolocationValidationDto(
                        true,
                        true,
                        null,
                        GeofenceMode.Disabled,
                        "GeofenceDisabled",
                        "Geofence desativada.")));
        }
    }
}
