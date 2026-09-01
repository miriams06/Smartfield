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

    public async Task<AttendancePunchDto> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/attendance/punch",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<AttendancePunchDto>(
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
            ?? $"A picagem falhou com o estado {(int)response.StatusCode}.";

        return new AttendanceApiException(response.StatusCode, message);
    }
}
