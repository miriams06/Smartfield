using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.Projects;

namespace SmartField.Client.Services;

public sealed class ProjectApiClient
{
    private readonly HttpClient httpClient;

    public ProjectApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ProjectDto>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/projects"
            : $"api/projects?search={Uri.EscapeDataString(search.Trim())}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<List<ProjectDto>>(response, cancellationToken);
    }

    public async Task<ProjectDto> GetAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/projects/{projectId}",
            cancellationToken);

        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
    }

    public async Task<ProjectDto> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/projects",
            request,
            cancellationToken);

        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
    }

    public async Task<ProjectDto> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/projects/{projectId}",
            request,
            cancellationToken);

        return await ReadRequiredAsync<ProjectDto>(response, cancellationToken);
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

            return value ?? throw new ProjectApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new ProjectApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<ProjectApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ProjectApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProjectApiProblemDetails>(
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

        return new ProjectApiException(response.StatusCode, message);
    }
}
