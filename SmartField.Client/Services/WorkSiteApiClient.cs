using System.Net.Http.Json;
using SmartField.Client.WorkSites;

namespace SmartField.Client.Services;

public sealed class WorkSiteApiClient
{
    private readonly HttpClient httpClient;

    public WorkSiteApiClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<IReadOnlyList<WorkSiteDto>> SearchAsync(string? search, CancellationToken cancellationToken)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/worksites"
            : $"api/worksites?search={Uri.EscapeDataString(search.Trim())}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<List<WorkSiteDto>>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> GetAsync(Guid workSiteId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/worksites/{workSiteId}", cancellationToken);
        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> CreateAsync(CreateWorkSiteRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/worksites", request, cancellationToken);
        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> UpdateAsync(Guid workSiteId, UpdateWorkSiteRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/worksites/{workSiteId}", request, cancellationToken);
        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
    }

    private static Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        return ApiResponseReader.ReadRequiredAsync<T, WorkSiteApiException>(
            response,
            static (statusCode, message, correlationId) => new WorkSiteApiException(statusCode, message, correlationId),
            cancellationToken);
    }
}
