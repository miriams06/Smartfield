using SmartField.Domain.Entities;
using SmartField.Domain.Enums;

namespace SmartField.Application.Attendance;

public interface IAttendanceStore
{
    Task<bool> EmployeeCanPunchAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<bool> ProjectExistsAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken);

    Task<AttendanceEvent?> GetByClientEventIdAsync(
        Guid companyId,
        Guid employeeId,
        Guid clientEventId,
        CancellationToken cancellationToken);

    Task<AttendanceEventType?> GetLastEventTypeAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<string?> GetCompanyTimeZoneAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
        Guid companyId,
        Guid? employeeId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
        Guid companyId,
        Guid employeeId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
        Guid companyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? employeeId,
        CancellationToken cancellationToken);

    void Add(AttendanceEvent attendanceEvent);

    void Add(AuditLog auditLog);

    void Add(IntegrationOutbox integrationOutbox);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
