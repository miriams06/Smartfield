using System.Net.Http.Json;
using SmartField.Client.Projects;

namespace SmartField.Client.Services;

public sealed class ProjectApiClient
{
    private readonly HttpClient httpClient;

    public ProjectApiClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<IReadOnlyList<ProjectDto>> SearchAsync(string? search, CancellationToken cancellationToken)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/projects"
            : $"api/projects?search={Uri.EscapeDataString(search.Trim())}";
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<List<ProjectDto>>(response, cancellationToken);
    }

    public async Task<ProjectDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/projects/{projectId}", cancellationToken);
        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/projects", request, cancellationToken);
        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
    }

    public async Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/projects/{projectId}", request, cancellationToken);
        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
    }

    private static Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        return ApiResponseReader.ReadRequiredAsync<T, ProjectApiException>(
            response,
            static (statusCode, message, correlationId) => new ProjectApiException(statusCode, message, correlationId),
            cancellationToken);
    }
}
