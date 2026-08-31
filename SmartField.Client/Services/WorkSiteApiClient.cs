using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.WorkSites;

namespace SmartField.Client.Services;

public sealed class WorkSiteApiClient
{
    private readonly HttpClient httpClient;

    public WorkSiteApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<WorkSiteDto>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/worksites"
            : $"api/worksites?search={Uri.EscapeDataString(search.Trim())}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<List<WorkSiteDto>>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> GetAsync(
        Guid workSiteId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/worksites/{workSiteId}",
            cancellationToken);

        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> CreateAsync(
        CreateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/worksites",
            request,
            cancellationToken);

        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
    }

    public async Task<WorkSiteDto> UpdateAsync(
        Guid workSiteId,
        UpdateWorkSiteRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/worksites/{workSiteId}",
            request,
            cancellationToken);

        return await ReadRequiredAsync<WorkSiteDto>(response, cancellationToken);
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

            return value ?? throw new WorkSiteApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new WorkSiteApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<WorkSiteApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        WorkSiteApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<WorkSiteApiProblemDetails>(
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

        return new WorkSiteApiException(response.StatusCode, message);
    }
}
