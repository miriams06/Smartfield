using Microsoft.EntityFrameworkCore;
using SmartField.Infrastructure.Attendance;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Tests;

public class AttendanceStoreQueryTests
{
    private static readonly Guid CompanyId =
        Guid.Parse("9f0b4a28-864b-4d2f-9ca6-44cf64352d68");
    private static readonly Guid EmployeeId =
        Guid.Parse("70bfeaba-236d-48b0-b9ab-a3f8cb22d389");

    [Fact]
    public void BuildBackofficeEmployeesQuery_IsTranslatableBySqlServerProvider()
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=SmartField_QueryTranslation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new SmartFieldDbContext(options)
        {
            CurrentCompanyId = CompanyId
        };
        var store = new AttendanceStore(context);

        var sql = store.BuildBackofficeEmployeesQuery(CompanyId, EmployeeId)
            .ToQueryString();

        Assert.Contains("FROM [Employees]", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void BuildEventsBetweenQuery_IsTranslatableBySqlServerProvider()
    {
        var options = new DbContextOptionsBuilder<SmartFieldDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=SmartField_QueryTranslation;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new SmartFieldDbContext(options)
        {
            CurrentCompanyId = CompanyId
        };
        var store = new AttendanceStore(context);

        var sql = store.BuildEventsBetweenQuery(
                CompanyId,
                new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
                EmployeeId)
            .ToQueryString();

        Assert.Contains("FROM [AttendanceEvents]", sql);
        Assert.Contains("ORDER BY", sql);
    }
}
