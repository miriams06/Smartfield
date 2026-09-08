using System.Text.Json;
using SmartField.Domain.Entities;

namespace SmartField.Application.Attendance;

public static class DailyWorkReportService
{
    public static string? ValidateSummary(string? summary)
    {
        var trimmed = summary?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "O resumo do dia é obrigatório antes de registar a saída.";
        if (trimmed.Length < 10)
            return "O resumo do dia deve ter pelo menos 10 caracteres.";
        if (summary!.Length > 4000)
            return "O resumo do dia não pode exceder 4000 caracteres.";
        return null;
    }

    // Stages the report and its audit only; AttendanceService saves them with the punch.
    public static async Task SubmitAsync(
        IAttendanceStore store, AttendanceEvent clockOut, Guid userId, string summary,
        DateOnly workDate, CancellationToken cancellationToken)
    {
        var report = await store.GetDailyWorkReportAsync(
            clockOut.CompanyId, clockOut.EmployeeId, workDate, cancellationToken);
        var oldValues = report is null ? null : Serialize(report);
        if (report is null)
        {
            report = new DailyWorkReport
            {
                CompanyId = clockOut.CompanyId,
                EmployeeId = clockOut.EmployeeId,
                WorkDate = workDate,
                CreatedAtUtc = clockOut.ServerTimestampUtc
            };
            store.Add(report);
        }
        else
        {
            report.UpdatedAtUtc = clockOut.ServerTimestampUtc;
        }
        report.Summary = summary;
        report.ClockOutAttendanceEventId = clockOut.Id;
        report.SubmittedAtUtc = clockOut.ServerTimestampUtc;
        store.Add(new AuditLog
        {
            CompanyId = clockOut.CompanyId,
            UserId = userId,
            EntityType = nameof(DailyWorkReport),
            EntityId = report.Id,
            Action = oldValues is null ? "Created" : "Updated",
            OldValues = oldValues,
            NewValues = Serialize(report),
            TimestampUtc = clockOut.ServerTimestampUtc,
            CreatedAtUtc = clockOut.ServerTimestampUtc
        });
    }

    private static string Serialize(DailyWorkReport report) => JsonSerializer.Serialize(new
    {
        report.Id,
        report.EmployeeId,
        report.WorkDate,
        report.Summary,
        report.ClockOutAttendanceEventId,
        report.SubmittedAtUtc
    });
}
