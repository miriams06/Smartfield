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

    private sealed class PunchHandler : HttpMessageHandler
    {
        public List<AttendancePunchRequest> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                var punch = (await request.Content!.ReadFromJsonAsync<AttendancePunchRequest>(cancellationToken))!;
                Requests.Add(punch);
                if (Requests.Count == 1) throw new HttpRequestException("Response lost");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new AttendancePunchDto(
                    Guid.NewGuid(), Guid.NewGuid(), "ClockOut", punch.ClientEventId, DateTimeOffset.UtcNow,
                    punch.ClientTimestampUtc, null, null, null, null, null, null, null, true)) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new AttendanceStateDto(
                Guid.NewGuid(), "Employee", "Closed", "DIA FECHADO", "2026-09-08", "ClockOut", ["ClockIn"], null, 0, 0, 0, DateTimeOffset.UtcNow)) };
        }
    }
}
