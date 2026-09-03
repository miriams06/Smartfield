using System.Net.Http.Json;
using System.Text.Json;
using SmartField.Client.Employees;

namespace SmartField.Client.Services;

public sealed class EmployeeApiClient
{
    private readonly HttpClient httpClient;

    public EmployeeApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<IReadOnlyList<EmployeeDto>> SearchAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var requestUri = string.IsNullOrWhiteSpace(search)
            ? "api/employees"
            : $"api/employees?search={Uri.EscapeDataString(search.Trim())}";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<List<EmployeeDto>>(response, cancellationToken);
    }

    public async Task<EmployeeDto> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/employees/{employeeId}",
            cancellationToken);

        return await ReadRequiredAsync<EmployeeDto>(response, cancellationToken);
    }

    public async Task<EmployeeOptions> GetOptionsAsync(
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var requestUri = employeeId.HasValue
            ? $"api/employees/options?employeeId={employeeId.Value}"
            : "api/employees/options";

        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        return await ReadRequiredAsync<EmployeeOptions>(response, cancellationToken);
    }

    public async Task<EmployeeDto> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/employees",
            request,
            cancellationToken);

        return await ReadRequiredAsync<EmployeeDto>(response, cancellationToken);
    }

    public async Task<EmployeeDto> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/employees/{employeeId}",
            request,
            cancellationToken);

        return await ReadRequiredAsync<EmployeeDto>(response, cancellationToken);
    }

    public async Task<EmployeeDto> CreateUserAsync(
        Guid employeeId,
        CreateEmployeeUserRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/employees/{employeeId}/user",
            request,
            cancellationToken);

        return await ReadRequiredAsync<EmployeeDto>(response, cancellationToken);
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

            return value ?? throw new EmployeeApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new EmployeeApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<EmployeeApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(
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

        return new EmployeeApiException(response.StatusCode, message);
    }
}
