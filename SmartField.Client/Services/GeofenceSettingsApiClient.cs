using System.Net.Http.Json;
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

    private static Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        return ApiResponseReader.ReadRequiredAsync<T, GeolocationApiException>(
            response,
            static (statusCode, message, correlationId) =>
                new GeolocationApiException(statusCode, message, correlationId),
            cancellationToken);
    }
}
