using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.Attendance;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Attendance;

public sealed class AttendanceStore : IAttendanceStore
{
    private readonly SmartFieldDbContext dbContext;

    public AttendanceStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task<bool> EmployeeCanPunchAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Employees
            .AsNoTracking()
            .AnyAsync(
                employee =>
                    employee.CompanyId == companyId
                    && employee.Id == employeeId
                    && employee.IsActive,
                cancellationToken);
    }

    public Task<bool> ProjectExistsAsync(
        Guid companyId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.CompanyId == companyId
                    && project.Id == projectId,
                cancellationToken);
    }

    public Task<AttendanceEvent?> GetByClientEventIdAsync(
        Guid companyId,
        Guid employeeId,
        Guid clientEventId,
        CancellationToken cancellationToken)
    {
        return dbContext.AttendanceEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.EmployeeId == employeeId
                    && attendanceEvent.ClientEventId == clientEventId,
                cancellationToken);
    }

    public Task<AttendanceEventType?> GetLastEventTypeAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        return dbContext.AttendanceEvents
            .AsNoTracking()
            .Where(attendanceEvent =>
                attendanceEvent.CompanyId == companyId
                && attendanceEvent.EmployeeId == employeeId)
            .OrderByDescending(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenByDescending(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenByDescending(attendanceEvent => attendanceEvent.Id)
            .Select(attendanceEvent => (AttendanceEventType?)attendanceEvent.EventType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(AttendanceEvent attendanceEvent)
    {
        dbContext.AttendanceEvents.Add(attendanceEvent);
    }

    public void Add(AuditLog auditLog)
    {
        dbContext.AuditLogs.Add(auditLog);
    }

    public void Add(IntegrationOutbox integrationOutbox)
    {
        dbContext.IntegrationOutbox.Add(integrationOutbox);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
                && sqlException.Message.Contains(
                    "IX_AttendanceEvents_ClientEventId",
                    StringComparison.Ordinal))
        {
            throw new AttendanceClientEventConflictException(
                "Já existe uma picagem com este ClientEventId.",
                exception);
        }
    }
}
