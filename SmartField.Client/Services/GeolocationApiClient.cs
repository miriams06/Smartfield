using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.Geolocation;

namespace SmartField.Client.Services;

public sealed class GeolocationApiClient
{
    private readonly HttpClient httpClient;

    public GeolocationApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<GeolocationValidationDto> ValidateAsync(
        GeolocationValidationRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/geolocation/validate",
            request,
            cancellationToken);

        return await ReadRequiredAsync<GeolocationValidationDto>(
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

            return value ?? throw new GeolocationApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new GeolocationApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<GeolocationApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        GeolocationApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<GeolocationApiProblemDetails>(
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            // A API pode não devolver ProblemDetails em falhas de infraestrutura.
        }

        var validationMessage = problem?.Errors?
            .SelectMany(pair => pair.Value)
            .FirstOrDefault();

        var message = validationMessage
            ?? problem?.Detail
            ?? problem?.Title
            ?? $"O pedido falhou com o estado {(int)response.StatusCode}.";

        return new GeolocationApiException(response.StatusCode, message);
    }
}
