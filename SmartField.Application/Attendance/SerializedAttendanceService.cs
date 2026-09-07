using SmartField.Application.Abstractions;

namespace SmartField.Application.Attendance;

public sealed class SerializedAttendanceService : IAttendanceService
{
    private readonly AttendanceService innerService;
    private readonly IAttendancePunchConcurrencyGate concurrencyGate;
    private readonly ICurrentCompanyProvider currentCompanyProvider;
    private readonly ICurrentUserProvider currentUserProvider;

    public SerializedAttendanceService(
        AttendanceService innerService,
        IAttendancePunchConcurrencyGate concurrencyGate,
        ICurrentCompanyProvider currentCompanyProvider,
        ICurrentUserProvider currentUserProvider)
    {
        this.innerService = innerService;
        this.concurrencyGate = concurrencyGate;
        this.currentCompanyProvider = currentCompanyProvider;
        this.currentUserProvider = currentUserProvider;
    }

    public Task<AttendanceResult<AttendanceStateDto>> GetStateAsync(
        CancellationToken cancellationToken) =>
        innerService.GetStateAsync(cancellationToken);

    public Task<AttendanceResult<AttendanceTodayDto>> GetTodayAsync(
        CancellationToken cancellationToken) =>
        innerService.GetTodayAsync(cancellationToken);

    public Task<AttendanceResult<IReadOnlyList<AttendanceHistoryDayDto>>> GetHistoryAsync(
        CancellationToken cancellationToken) =>
        innerService.GetHistoryAsync(cancellationToken);

    public Task<AttendanceResult<AttendanceDayDetailDto>> GetDayAsync(
        DateOnly date,
        CancellationToken cancellationToken) =>
        innerService.GetDayAsync(date, cancellationToken);

    public Task<AttendanceResult<AttendanceBackofficeDayDto>> GetBackofficeDayAsync(
        AttendanceBackofficeDayFilter filter,
        CancellationToken cancellationToken) =>
        innerService.GetBackofficeDayAsync(filter, cancellationToken);

    public Task<AttendanceResult<AttendanceBackofficeCsvExportDto>> ExportBackofficeCsvAsync(
        AttendanceBackofficeExportFilter filter,
        CancellationToken cancellationToken) =>
        innerService.ExportBackofficeCsvAsync(filter, cancellationToken);

    public Task<AttendanceResult<AttendanceBackofficeDayDetailDto>> GetBackofficeDayDetailAsync(
        Guid employeeId,
        DateOnly date,
        CancellationToken cancellationToken) =>
        innerService.GetBackofficeDayDetailAsync(employeeId, date, cancellationToken);

    public Task<AttendanceResult<AttendanceCorrectionDto>> CorrectBackofficeEventAsync(
        Guid attendanceEventId,
        AttendanceCorrectionRequest request,
        CancellationToken cancellationToken) =>
        innerService.CorrectBackofficeEventAsync(
            attendanceEventId,
            request,
            cancellationToken);

    public Task<AttendanceResult<AttendancePunchDto>> PunchAsync(
        AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = currentCompanyProvider.CompanyId;
        var employeeId = currentUserProvider.EmployeeId;

        if (!companyId.HasValue || !employeeId.HasValue)
        {
            return innerService.PunchAsync(request, cancellationToken);
        }

        return concurrencyGate.ExecuteAsync(
            companyId.Value,
            employeeId.Value,
            token => innerService.PunchAsync(request, token),
            cancellationToken);
    }
}
