using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using SmartField.Client.Attendance;

namespace SmartField.Client.Services;

public sealed class AttendanceApiClient
{
    private readonly HttpClient httpClient;

    public AttendanceApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<AttendanceStateDto> GetStateAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/state",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceStateDto>(
            response,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceProjectOptionDto>> GetPunchProjectsAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/projects",
            cancellationToken);

        return await ReadRequiredAsync<List<AttendanceProjectOptionDto>>(
            response,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceHistoryDayDto>> GetHistoryAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/history",
            cancellationToken);

        return await ReadRequiredAsync<List<AttendanceHistoryDayDto>>(
            response,
            cancellationToken);
    }

    public async Task<AttendanceDayDetailDto> GetDayAsync(
        string date,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/attendance/day/{Uri.EscapeDataString(date)}",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceDayDetailDto>(
            response,
            cancellationToken);
    }

    public async Task<AttendanceBackofficeDayDto> GetBackofficeDayAsync(
        string date,
        Guid? employeeId,
        Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"date={Uri.EscapeDataString(date)}"
        };

        if (employeeId.HasValue)
        {
            query.Add($"employeeId={employeeId.Value}");
        }

        if (workSiteId.HasValue)
        {
            query.Add($"workSiteId={workSiteId.Value}");
        }

        using var response = await httpClient.GetAsync(
            $"api/attendance/admin/day?{string.Join("&", query)}",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceBackofficeDayDto>(
            response,
            cancellationToken);
    }

    public async Task<AttendanceCsvExportDto> ExportBackofficeCsvAsync(
        string fromDate,
        string toDate,
        Guid? employeeId,
        Guid? workSiteId,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"fromDate={Uri.EscapeDataString(fromDate)}",
            $"toDate={Uri.EscapeDataString(toDate)}"
        };

        if (employeeId.HasValue)
        {
            query.Add($"employeeId={employeeId.Value}");
        }

        if (workSiteId.HasValue)
        {
            query.Add($"workSiteId={workSiteId.Value}");
        }

        using var response = await httpClient.GetAsync(
            $"api/attendance/admin/export.csv?{string.Join("&", query)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, cancellationToken);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var fileName = GetFileName(response.Content.Headers.ContentDisposition)
            ?? $"smartfield-attendance-{fromDate}-{toDate}.csv";
        var contentType = response.Content.Headers.ContentType?.ToString()
            ?? "text/csv; charset=utf-8";

        return new AttendanceCsvExportDto(fileName, contentType, content);
    }

    public async Task<AttendanceBackofficeDayDetailDto> GetBackofficeDayDetailAsync(
        string date,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/attendance/admin/day/{Uri.EscapeDataString(date)}/employees/{employeeId}",
            cancellationToken);

        return await ReadRequiredAsync<AttendanceBackofficeDayDetailDto>(
            response,
            cancellationToken);
    }

    public async Task<AttendanceCorrectionDto> CorrectBackofficeEventAsync(
        Guid attendanceEventId,
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/attendance/admin/events/{attendanceEventId}/corrections",
            request,
            cancellationToken);

        return await ReadRequiredAsync<AttendanceCorrectionDto>(
            response,
            cancellationToken);
    }

    public async Task<AttendancePunchDto> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/attendance/punch",
            request,
            cancellationToken);

        return await ReadRequiredAsync<AttendancePunchDto>(
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

            return value ?? throw new AttendanceApiException(
                response.StatusCode,
                "A API devolveu uma resposta vazia.");
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            throw new AttendanceApiException(
                response.StatusCode,
                "A API devolveu uma resposta inválida.");
        }
    }

    private static async Task<AttendanceApiException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        AttendanceProblemDetails? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<AttendanceProblemDetails>(
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
            when (exception is JsonException or NotSupportedException)
        {
            // Algumas falhas de infraestrutura podem não devolver ProblemDetails.
        }

        var validationMessage = problem?.Errors?
            .SelectMany(pair => pair.Value)
            .FirstOrDefault();

        var message = validationMessage
            ?? problem?.Detail
            ?? problem?.Title
            ?? $"O pedido de assiduidade falhou com o estado {(int)response.StatusCode}.";

        return new AttendanceApiException(response.StatusCode, message);
    }

    private static string? GetFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        return contentDisposition?.FileNameStar
            ?? contentDisposition?.FileName?.Trim('"');
    }
}
