using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SmartField.Api.Authentication;
using SmartField.Application.Attendance;
using SmartField.Application.Geolocation;
using SmartField.Application.WorkSites;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Identity;

namespace SmartField.Api.Tests;

[Trait("Category", "Integration")]
[Trait("Layer", "Http")]
public class ApiHttpTests
{
    [SqlServerIntegrationFact]
    public async Task Authentication_ValidInvalidInactiveAndAnonymousRequests()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var anonymous = factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/attendance/state")).StatusCode);
        foreach (var request in new[]
        {
            new LoginRequest("employee.a@example.test", "invalid"),
            new LoginRequest("missing@example.test", factory.Password),
            new LoginRequest("inactive@example.test", factory.Password)
        })
        {
            using var rejected = await anonymous.PostAsJsonAsync("/api/auth/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            Assert.DoesNotContain("accessToken", await rejected.Content.ReadAsStringAsync());
        }
        using var employee = await factory.LoginAsync("employee.a@example.test");
        var me = await employee.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(factory.CompanyA, me!.CompanyId);
        Assert.Equal(factory.EmployeeA, me.EmployeeId);
        Assert.Contains(SmartFieldRoles.Employee, me.Roles);
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/attendance/state")).StatusCode);
        await factory.WithDatabaseAsync(async db =>
        {
            (await db.Employees.IgnoreQueryFilters().SingleAsync(x => x.Id == factory.EmployeeA)).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/attendance/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.PostAsJsonAsync("/api/attendance/punch", Punch("ClockIn", factory.SiteA))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest("employee.a@example.test", factory.Password))).StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Authorization_EmployeeDeniedAndManagerAllowedInBackoffice()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var employee = await factory.LoginAsync("employee.a@example.test");
        using var manager = await factory.LoginAsync("manager.a@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/geofence-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/geofence-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/employees/{factory.EmployeeB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/worksites/{factory.SiteB}")).StatusCode);
        using var update = await manager.PutAsJsonAsync($"/api/worksites/{factory.SiteB}",
            new UpdateWorkSiteRequest("B-SITE", "Tampered", null, 0, 0, 100, true, null));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        await factory.WithDatabaseAsync(async db =>
            Assert.Equal("B Site", (await db.WorkSites.IgnoreQueryFilters().SingleAsync(x => x.Id == factory.SiteB)).Name));
    }

    [SqlServerIntegrationFact]
    public async Task Attendance_FullDayPersistsOneReportAndExposesItToEmployeeAndManager()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var employee = await factory.LoginAsync("employee.a@example.test");
        using var manager = await factory.LoginAsync("manager.a@example.test");
        const string summary = "  Instalação concluída.\nEquipamento testado.  ";
        AttendancePunchDto? clockOut = null;
        foreach (var eventType in new[] { "ClockIn", "BreakStart", "BreakEnd", "ClockOut" })
        {
            var request = Punch(eventType, factory.SiteA) with { DailySummary = eventType == "ClockOut" ? summary : null };
            using var response = await employee.PostAsJsonAsync("/api/attendance/punch", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<AttendancePunchDto>();
            Assert.Equal(eventType, result!.EventType);
            Assert.Equal(factory.EmployeeA, result.EmployeeId);
            Assert.False(result.IsDuplicate);
            clockOut = result;
        }
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clockOut!.ServerTimestampUtc,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon")).DateTime);
        var day = await employee.GetFromJsonAsync<AttendanceDayDetailDto>($"/api/attendance/day/{date:yyyy-MM-dd}");
        Assert.Equal(summary, day!.DailySummary);
        Assert.Equal(4, day.Events.Count);
        var backoffice = await manager.GetFromJsonAsync<AttendanceBackofficeDayDetailDto>(
            $"/api/attendance/admin/day/{date:yyyy-MM-dd}/employees/{factory.EmployeeA}");
        Assert.Equal(summary, backoffice!.DailySummary);
        Assert.Equal(4, backoffice.Events.Count);
        await factory.WithDatabaseAsync(async db =>
        {
            var report = Assert.Single(await db.DailyWorkReports.IgnoreQueryFilters().ToListAsync());
            Assert.Equal(factory.CompanyA, report.CompanyId);
            Assert.Equal(factory.EmployeeA, report.EmployeeId);
            Assert.Equal(date, report.WorkDate);
            Assert.Equal(summary, report.Summary);
            Assert.Equal(clockOut.Id, report.ClockOutAttendanceEventId);
            Assert.Equal(4, await db.AttendanceEvents.IgnoreQueryFilters().CountAsync());
        });
        using var other = await factory.LoginAsync("employee.b@example.test");
        var otherDay = await other.GetFromJsonAsync<AttendanceDayDetailDto>($"/api/attendance/day/{date:yyyy-MM-dd}?employeeId={factory.EmployeeA}&companyId={factory.CompanyA}");
        Assert.Null(otherDay!.DailySummary);
        Assert.Empty(otherDay.Events);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/attendance/admin/day/{date:yyyy-MM-dd}/employees/{factory.EmployeeA}")).StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task Attendance_InvalidSequenceAndMissingSummaryWriteNothing_DuplicateIsIdempotent()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var employee = await factory.LoginAsync("employee.a@example.test");
        using var invalid = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("BreakEnd", factory.SiteA));
        Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
        await factory.WithDatabaseAsync(async db => Assert.Empty(await db.AttendanceEvents.IgnoreQueryFilters().ToListAsync()));
        var request = Punch("ClockIn", factory.SiteA);
        using var first = await employee.PostAsJsonAsync("/api/attendance/punch", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadFromJsonAsync<AttendancePunchDto>();
        using var repeated = await employee.PostAsJsonAsync("/api/attendance/punch", request);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var duplicate = await repeated.Content.ReadFromJsonAsync<AttendancePunchDto>();
        Assert.True(duplicate!.IsDuplicate);
        Assert.Equal(original!.Id, duplicate.Id);
        using var missingSummary = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("ClockOut", factory.SiteA) with { DailySummary = null });
        Assert.Equal(HttpStatusCode.BadRequest, missingSummary.StatusCode);
        await factory.WithDatabaseAsync(async db =>
        {
            Assert.Single(await db.AttendanceEvents.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.DailyWorkReports.IgnoreQueryFilters().ToListAsync());
        });
    }

    [SqlServerIntegrationFact]
    public async Task Geofence_AllModesAndPoorAccuracyAreEnforcedThroughHttp()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var employee = await factory.LoginAsync("employee.a@example.test");
        using var manager = await factory.LoginAsync("manager.a@example.test");
        foreach (var (mode, eventType, accepted, inside) in new[]
        {
            (GeofenceMode.Disabled, "ClockIn", true, (bool?)null),
            (GeofenceMode.Warning, "BreakStart", true, (bool?)false),
            (GeofenceMode.Block, "BreakEnd", false, (bool?)null)
        })
        {
            using var config = await manager.PutAsJsonAsync("/api/geofence-settings", new UpdateGeofenceSettingsRequest(false, mode, 100, 100));
            Assert.Equal(HttpStatusCode.OK, config.StatusCode);
            using var response = await employee.PostAsJsonAsync("/api/attendance/punch", Punch(eventType, factory.SiteA) with { Latitude = 41.15m, Longitude = -8.61m });
            Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
            if (accepted) Assert.Equal(inside, (await response.Content.ReadFromJsonAsync<AttendancePunchDto>())!.IsInsideGeofence);
        }
        foreach (var mode in new[] { GeofenceMode.Warning, GeofenceMode.Block })
        {
            using var config = await manager.PutAsJsonAsync("/api/geofence-settings", new UpdateGeofenceSettingsRequest(false, mode, 100, 100));
            Assert.Equal(HttpStatusCode.OK, config.StatusCode);
            using var response = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("BreakEnd", factory.SiteA) with { AccuracyMeters = 900 });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
            Assert.Contains("precisão suficiente", problem!.Detail);
        }
        await factory.WithDatabaseAsync(async db => Assert.Equal(2, await db.AttendanceEvents.IgnoreQueryFilters().CountAsync()));
        using var insideResponse = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("BreakEnd", factory.SiteA));
        Assert.Equal(HttpStatusCode.OK, insideResponse.StatusCode);
        Assert.True((await insideResponse.Content.ReadFromJsonAsync<AttendancePunchDto>())!.IsInsideGeofence);
    }

    [SqlServerIntegrationFact]
    public async Task Security_ForeignGuidsAndManipulatedIdentityCannotCrossCompanies()
    {
        await using var factory = await ApiHttpTestFactory.StartAsync();
        using var employee = await factory.LoginAsync("employee.a@example.test");
        using var foreignSite = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("ClockIn", factory.SiteB));
        Assert.Equal(HttpStatusCode.NotFound, foreignSite.StatusCode);
        using var foreignProject = await employee.PostAsJsonAsync("/api/attendance/punch", Punch("ClockIn", factory.SiteA) with { ProjectId = factory.ProjectB });
        Assert.Equal(HttpStatusCode.NotFound, foreignProject.StatusCode);
        await factory.WithDatabaseAsync(async db => Assert.Empty(await db.AttendanceEvents.IgnoreQueryFilters().ToListAsync()));
        // Extra browser fields must never override the authenticated company or employee.
        using var spoofed = await employee.PostAsJsonAsync("/api/attendance/punch", new
        {
            eventType = "ClockIn", clientEventId = Guid.NewGuid(), clientTimestampUtc = DateTimeOffset.UtcNow,
            workSiteId = factory.SiteA, latitude = 38.72m, longitude = -9.14m, accuracyMeters = 10,
            companyId = factory.CompanyB, employeeId = factory.EmployeeB
        });
        Assert.Equal(HttpStatusCode.OK, spoofed.StatusCode);
        Assert.Equal(factory.EmployeeA, (await spoofed.Content.ReadFromJsonAsync<AttendancePunchDto>())!.EmployeeId);
        await factory.WithDatabaseAsync(async db =>
        {
            var persisted = Assert.Single(await db.AttendanceEvents.IgnoreQueryFilters().ToListAsync());
            Assert.Equal(factory.CompanyA, persisted.CompanyId);
            Assert.Equal(factory.EmployeeA, persisted.EmployeeId);
        });
        // Change a real token's company claim without being able to re-sign it.
        var parts = employee.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
        var payload = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(parts[1]))!;
        payload[SmartFieldClaimTypes.CompanyId] = factory.CompanyB.ToString();
        parts[1] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        employee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/attendance/state")).StatusCode);
    }

    private static AttendancePunchRequest Punch(string eventType, Guid siteId) => new(
        eventType, Guid.NewGuid(), DateTimeOffset.UtcNow, 38.72m, -9.14m, 10, siteId, null,
        eventType == "ClockOut" ? "Trabalho concluído durante o dia." : null);
}
