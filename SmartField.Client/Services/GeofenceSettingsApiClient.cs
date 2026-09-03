using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.Geolocation;

namespace SmartField.Client.Services;

public sealed class GeofenceSettingsApiClient
{
    private readonly HttpClient httpClient;

    public GeofenceSettingsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<GeofenceSettingsDto> GetAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/geofence-settings",
            cancellationToken);

        return await ReadRequiredAsync<GeofenceSettingsDto>(
            response,
            cancellationToken);
    }

    public async Task<GeofenceSettingsDto> UpdateAsync(
        UpdateGeofenceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            "api/geofence-settings",
            request,
            cancellationToken);

        return await ReadRequiredAsync<GeofenceSettingsDto>(
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
