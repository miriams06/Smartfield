namespace SmartField.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceResult<AttendanceStateDto>> GetStateAsync(
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendanceTodayDto>> GetTodayAsync(
        CancellationToken cancellationToken);

    Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken);
}
