namespace SmartField.Application.Attendance;

public interface IAttendancePunchConcurrencyGate
{
    Task<T> ExecuteAsync<T>(
        Guid companyId,
        Guid employeeId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
