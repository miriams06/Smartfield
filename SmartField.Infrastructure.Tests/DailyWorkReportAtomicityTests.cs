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

public class DailyWorkReportAtomicityTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid UserId =
        Guid.Parse("4a290c06-2a4b-4f22-a2df-76111c8d055b");
    private static readonly DateTimeOffset ServerNow =
        new(2026, 9, 8, 17, 0, 0, TimeSpan.Zero);

    [SqlServerIntegrationFact]
    [Trait("Category", "Integration")]
    public async Task ClockOutFailure_DoesNotPersistDailyReportAuditOrOutbox()
    {
        var connectionString = CreateConnectionString();

        try
        {
            var employeeId = await InitializeDatabaseAsync(connectionString);
            await SeedClockInAsync(connectionString, employeeId);

            await using (var context = CreateContext(connectionString))
            {
                await context.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE AttendanceEvents ADD CONSTRAINT TestRejectClockOut CHECK (EventType <> N'ClockOut')");

                var service = CreateService(context, employeeId);

                await Assert.ThrowsAsync<DbUpdateException>(() => service.PunchAsync(
                    new AttendancePunchRequest(
                        "ClockOut",
                        Guid.NewGuid(),
                        ServerNow,
                        null,
                        null,
                        null,
                        null,
                        null,
                        "Resumo válido para testar rollback da saída."),
                    CancellationToken.None));
            }

            await using var assertionContext = CreateContext(connectionString);
            var events = await assertionContext.AttendanceEvents
                .OrderBy(item => item.ServerTimestampUtc)
                .ToListAsync();

            var clockIn = Assert.Single(events);
            Assert.Equal(AttendanceEventType.ClockIn, clockIn.EventType);
            Assert.Empty(await assertionContext.DailyWorkReports.ToListAsync());
            Assert.Empty(await assertionContext.AuditLogs.ToListAsync());
            Assert.Empty(await assertionContext.IntegrationOutbox.ToListAsync());
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
        var builder = new SqlConnectionStringBuilder(
            SqlServerIntegrationTestConfiguration.ConnectionString)
        {
            InitialCatalog = $"SmartField_DailyReportAtomicity_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public Guid? CompanyId => DailyWorkReportAtomicityTests.CompanyId;
    }

    private sealed class FakeCurrentUserProvider(Guid employeeId) : ICurrentUserProvider
    {
        public Guid? UserId => DailyWorkReportAtomicityTests.UserId;
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
