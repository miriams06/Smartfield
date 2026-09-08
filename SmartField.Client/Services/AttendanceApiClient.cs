using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public async Task<IReadOnlyList<AttendanceWorkSiteOptionDto>> GetPunchWorkSitesAsync(
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            "api/attendance/worksites",
            cancellationToken);

        return await ReadRequiredAsync<List<AttendanceWorkSiteOptionDto>>(
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
            throw await ApiResponseReader.CreateExceptionAsync(
                response,
                static (statusCode, message, correlationId) =>
                    new AttendanceApiException(statusCode, message, correlationId),
                cancellationToken);
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

    private static Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        return ApiResponseReader.ReadRequiredAsync<T, AttendanceApiException>(
            response,
            static (statusCode, message, correlationId) =>
                new AttendanceApiException(statusCode, message, correlationId),
            cancellationToken);
    }

    private static string? GetFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        return contentDisposition?.FileNameStar
            ?? contentDisposition?.FileName?.Trim('"');
    }
}
