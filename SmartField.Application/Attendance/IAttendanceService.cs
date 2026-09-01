namespace SmartField.Application.Attendance;

public interface IAttendanceService
{
    Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken);
}
