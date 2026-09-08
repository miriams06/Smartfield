using System.Net.Http.Json;
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

    private static Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        return ApiResponseReader.ReadRequiredAsync<T, EmployeeApiException>(
            response,
            static (statusCode, message, correlationId) =>
                new EmployeeApiException(statusCode, message, correlationId),
            cancellationToken);
    }
}
