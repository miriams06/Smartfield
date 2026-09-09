using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.JSInterop;
using SmartField.Client.Attendance;
using SmartField.Client.Geolocation;
using SmartField.Client.Pages;
using SmartField.Client.Services;

namespace SmartField.Client.Tests;

public class DailySummaryFlowTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public async Task ClockOut_OpensFormWithoutPunchingOrRequestingLocation()
    {
        using var page = new Home();
        await OpenAsync(page);
        Assert.True(Get<bool>(page, "_showDailySummary"));
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.Null(Get<Guid?>(page, "_clockOutClientEventId"));
        Assert.True(Get<bool>(page, "_showDailySummary"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("           ")]
    [InlineData(" 123456789 ")]
    public async Task InvalidSummary_CannotSubmit(string summary)
    {
        using var page = new Home();
        Set(page, "_dailySummary", summary);
        await OpenAsync(page);
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.NotNull(typeof(Home).GetProperty("DailySummaryError", PrivateInstance)!.GetValue(page));
        Assert.Null(Get<Guid?>(page, "_clockOutClientEventId"));
    }

    [Fact]
    public async Task FailedSubmission_PreservesDraftAndReusesClientEventIdOnRetry()
    {
        using var handler = new PunchHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://smartfield.test/") };
        using var page = new Home();
        SelectWorkSite(page);
        typeof(Home).GetProperty("AttendanceApiClient", PrivateInstance)!.SetValue(page, new AttendanceApiClient(http));
        typeof(Home).GetProperty("BrowserGeolocationService", PrivateInstance)!.SetValue(page, new BrowserGeolocationService(new FakeJs()));
        const string summary = "  Trabalho concluído.\nSistema testado.  ";
        Set(page, "_dailySummary", summary);
        await OpenAsync(page);
        Assert.Empty(handler.Requests);
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.True(Get<bool>(page, "_showDailySummary"));
        Assert.Equal(summary, Get<string>(page, "_dailySummary"));
        Assert.NotNull(Get<string?>(page, "_errorMessage"));
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0].ClientEventId, handler.Requests[1].ClientEventId);
        Assert.All(handler.Requests, request => Assert.Equal(summary, request.DailySummary));
        Assert.False(Get<bool>(page, "_showDailySummary"));
        Assert.Equal(string.Empty, Get<string>(page, "_dailySummary"));
        Assert.Null(Get<string?>(page, "_warningMessage"));
    }

    [Fact]
    public async Task ImpreciseGps_ShowsServerMessageAndRetriesWithNewPositionAndPreservedSummary()
    {
        using var handler = new PunchHandler(rejectAccuracy: true);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://smartfield.test/") };
        using var page = new Home();
        SelectWorkSite(page);
        var gps = new ImprovingGps();
        typeof(Home).GetProperty("AttendanceApiClient", PrivateInstance)!.SetValue(page, new AttendanceApiClient(http));
        typeof(Home).GetProperty("BrowserGeolocationService", PrivateInstance)!.SetValue(page, new BrowserGeolocationService(gps));
        const string summary = "Trabalho realizado durante o dia.";
        Set(page, "_dailySummary", summary);
        await OpenAsync(page);
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.Contains("A localização ainda não tem precisão suficiente", Get<string>(page, "_errorMessage"));
        Assert.False(Get<bool>(page, "_submitting"));
        Assert.True(Get<bool>(page, "_showDailySummary"));
        Assert.Equal(summary, Get<string>(page, "_dailySummary"));
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.Equal(2, gps.Calls);
        Assert.Equal(900m, handler.Requests[0].AccuracyMeters);
        Assert.Equal(10m, handler.Requests[1].AccuracyMeters);
        Assert.Equal(handler.Requests[0].ClientEventId, handler.Requests[1].ClientEventId);
        Assert.False(Get<bool>(page, "_showDailySummary"));
    }

    private sealed class ImprovingGps : IJSRuntime
    {
        public int Calls { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult((TValue)(object)new BrowserGeolocationResult(BrowserGeolocationStatus.Success, 0, 0, ++Calls == 1 ? 900 : 10, null));
    }

    [Fact]
    public async Task NoSelectedOrDefaultWorkSite_StopsBeforeGpsAndApi()
    {
        using var page = new Home();
        Set(page, "_dailySummary", "Trabalho concluído durante o dia.");
        await OpenAsync(page);
        await InvokeAsync(page, "SubmitDailySummaryAsync");
        Assert.Equal("Seleciona um local de trabalho antes de registar a picagem.", Get<string>(page, "_errorMessage"));
        Assert.False(Get<bool>(page, "_submitting"));
        Assert.True(Get<bool>(page, "_showDailySummary"));
        Assert.Null(Get<Guid?>(page, "_clockOutClientEventId"));
    }

    private static void SelectWorkSite(Home page)
    {
        var id = Guid.NewGuid();
        Set(page, "_workSites", new AttendanceWorkSiteOptionDto[] { new(id, "SITE", "Site", null, false) });
        Set(page, "_selectedWorkSiteId", id);
    }

    private static Task OpenAsync(Home page)
    {
        var action = typeof(Home).GetMethod("ToDashboardAction", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, ["ClockOut"]);
        return InvokeAsync(page, "BeginActionAsync", action!);
    }

    private static Task InvokeAsync(Home page, string method, params object[] args) =>
        (Task)typeof(Home).GetMethod(method, PrivateInstance)!.Invoke(page, args)!;

    private static T Get<T>(Home page, string field) => (T)typeof(Home).GetField(field, PrivateInstance)!.GetValue(page)!;
    private static void Set(Home page, string field, object value) => typeof(Home).GetField(field, PrivateInstance)!.SetValue(page, value);

    private sealed class FakeJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromResult((TValue)(object)new BrowserGeolocationResult(BrowserGeolocationStatus.Success, 0, 0, 10, null));
    }

    private sealed class PunchHandler(bool rejectAccuracy = false) : HttpMessageHandler
    {
        public List<AttendancePunchRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                var punch = (await request.Content!.ReadFromJsonAsync<AttendancePunchRequest>(cancellationToken))!;
                Requests.Add(punch);
                if (Requests.Count == 1)
                {
                    if (rejectAccuracy) return new HttpResponseMessage(HttpStatusCode.Conflict)
                    { Content = JsonContent.Create(new { detail = "A localização ainda não tem precisão suficiente. Aguarda alguns segundos e tenta novamente." }) };
                    throw new HttpRequestException("Response lost");
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new AttendancePunchDto(
                    Guid.NewGuid(), Guid.NewGuid(), "ClockOut", punch.ClientEventId, DateTimeOffset.UtcNow,
                    punch.ClientTimestampUtc, null, null, null, null, null, null, null, true)) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new AttendanceStateDto(
                Guid.NewGuid(), "Employee", "Closed", "DIA FECHADO", "2026-09-08", "ClockOut", ["ClockIn"], null, 0, 0, 0, DateTimeOffset.UtcNow)) };
        }
    }
}
