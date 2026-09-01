using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.Attendance;

namespace SmartField.Client.Services;

public sealed class AttendanceApiClient
{
    private readonly HttpClient httpClient;

    public AttendanceApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<AttendanceStateDto> GetStateAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/state",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceStateDto>(
            response,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceHistoryDayDto>> GetHistoryAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/history",
            cancellationToken);

        return await ReadRequiredAsync<List<AttendanceHistoryDayDto>>(
            response,
            cancellationToken);
    }

    public async Task<AttendanceDayDetailDto> GetDayAsync(
        string date,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/attendance/day/{Uri.EscapeDataString(date)}",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceDayDetailDto>(
            response,
            cancellationToken);
    }

    public async Task<AttendancePunchDto> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/attendance/punch",
            request,
            cancellationToken);

        return await ReadRequiredAsync<AttendancePunchDto>(
            response,
            cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken: cancellationToken);

            return value ?? throw new AttendanceApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new AttendanceApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<AttendanceApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        AttendanceProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<AttendanceProblemDetails>(
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            // Algumas falhas de infraestrutura podem não devolver ProblemDetails.
        }

        var validationMessage = problem?.Errors?
            .SelectMany(pair => pair.Value)
            .FirstOrDefault();

        var message = validationMessage
            ?? problem?.Detail
            ?? problem?.Title
            ?? $"O pedido de assiduidade falhou com o estado {(int)response.StatusCode}.";

        return new AttendanceApiException(response.StatusCode, message);
    }
}
