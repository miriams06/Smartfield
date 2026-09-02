namespace SmartField.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceResult<AttendanceStateDto>> GetStateAsync(
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceTodayDto>> GetTodayAsync(
        CancellationToken cancellationToken);

    Task<AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>> GetHistoryAsync(
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceDayDetailDto>> GetDayAsync(
        DateOnly date,
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceBackofficeDayDto>> GetBackofficeDayAsync(
        AttendanceBackofficeDayFilter filter,
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceBackofficeCsvExportDto>> ExportBackofficeCsvAsync(
        AttendanceBackofficeExportFilter filter,
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceBackofficeDayDetailDto>> GetBackofficeDayDetailAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceCorrectionDto>> CorrectBackofficeEventAsync(
        Guid attendanceEventId,
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken);
}
