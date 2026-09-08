using System.Net.Http.Json;
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

        return await ApiResponseReader.ReadRequiredAsync<GeolocationValidationDto, GeolocationApiException>(
            response,
            static (statusCode, message, correlationId) =>
                new GeolocationApiException(statusCode, message, correlationId),
            cancellationToken);
    }
}
