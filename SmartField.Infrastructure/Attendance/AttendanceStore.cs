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

    public Task<AttendanceEvent?> GetEventAsync(
        Guid companyId,
        Guid attendanceEventId,
        CancellationToken cancellationToken)
    {
        return dbContext.AttendanceEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attendanceEvent =>
                    attendanceEvent.CompanyId == companyId
                    && attendanceEvent.Id == attendanceEventId,
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

    public Task<AttendanceEmployeeStateReference?> GetEmployeeStateReferenceAsync(
        Guid companyId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        return dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId
                && employee.Id == employeeId
                && employee.IsActive)
            .Select(employee => new AttendanceEmployeeStateReference(
                employee.Id,
                employee.Name,
                dbContext.Companies
                    .Where(company => company.Id == companyId)
                    .Select(company => company.TimeZone)
                    .Single()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetCompanyTimeZoneAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return dbContext.Companies
            .AsNoTracking()
            .Where(company =>
                company.Id == companyId
                && company.IsActive)
            .Select(company => company.TimeZone)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceBackofficeEmployeeReference>> GetBackofficeEmployeesAsync(
        Guid companyId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        return await BuildBackofficeEmployeesQuery(companyId, employeeId)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<AttendanceBackofficeEmployeeReference> BuildBackofficeEmployeesQuery(
        Guid companyId,
        Guid? employeeId)
    {
        var query = dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId
                && employee.IsActive);

        if (employeeId.HasValue)
        {
            query = query.Where(employee => employee.Id == employeeId.Value);
        }

        return query
            .OrderBy(employee => employee.Name)
            .ThenBy(employee => employee.EmployeeNumber)
            .Select(employee => new AttendanceBackofficeEmployeeReference(
                employee.Id,
                employee.EmployeeNumber,
                employee.Name,
                employee.DefaultWorkSiteId,
                employee.DefaultWorkSiteId.HasValue
                    ? dbContext.WorkSites
                        .Where(workSite =>
                            workSite.CompanyId == companyId
                            && workSite.Id == employee.DefaultWorkSiteId.Value)
                        .Select(workSite => workSite.Name)
                        .SingleOrDefault()
                    : null));
    }

    public async Task<IReadOnlyList<AttendanceEvent>> GetEventsFromAsync(
        Guid companyId,
        Guid employeeId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken)
    {
        return await dbContext.AttendanceEvents
            .AsNoTracking()
            .Where(attendanceEvent =>
                attendanceEvent.CompanyId == companyId
                && attendanceEvent.EmployeeId == employeeId
                && attendanceEvent.ServerTimestampUtc >= fromUtc)
            .OrderBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenBy(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenBy(attendanceEvent => attendanceEvent.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceEvent>> GetEventsBetweenAsync(
        Guid companyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        return await BuildEventsBetweenQuery(companyId, fromUtc, toUtc, employeeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceEventCorrectionReference>> GetCorrectionsForEventsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> attendanceEventIds,
        CancellationToken cancellationToken)
    {
        if (attendanceEventIds.Count == 0)
        {
            return [];
        }

        return await BuildCorrectionsForEventsQuery(companyId, attendanceEventIds)
            .ToListAsync(cancellationToken);
    }

    internal IQueryable<AttendanceEventCorrectionReference> BuildCorrectionsForEventsQuery(
        Guid companyId,
        IReadOnlyCollection<Guid> attendanceEventIds)
    {
        return dbContext.AttendanceCorrections
            .AsNoTracking()
            .Where(correction =>
                correction.CompanyId == companyId
                && attendanceEventIds.Contains(correction.AttendanceEventId))
            .OrderBy(correction => correction.AttendanceEventId)
            .ThenByDescending(correction => correction.CreatedAtUtc)
            .ThenByDescending(correction => correction.Id)
            .Select(correction => new AttendanceEventCorrectionReference(
                correction.Id,
                correction.AttendanceEventId,
                correction.OriginalTimestampUtc,
                correction.CorrectedTimestampUtc,
                correction.OriginalEventType,
                correction.CorrectedEventType,
                correction.Reason,
                correction.CorrectedByUserId,
                dbContext.Users
                    .Where(user =>
                        user.CompanyId == companyId
                        && user.Id == correction.CorrectedByUserId)
                    .Select(user => user.Email)
                    .SingleOrDefault(),
                correction.CreatedAtUtc));
    }

    internal IQueryable<AttendanceEvent> BuildEventsBetweenQuery(
        Guid companyId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? employeeId)
    {
        var query = dbContext.AttendanceEvents
            .AsNoTracking()
            .Where(attendanceEvent =>
                attendanceEvent.CompanyId == companyId
                && attendanceEvent.ServerTimestampUtc >= fromUtc
                && attendanceEvent.ServerTimestampUtc < toUtc);

        if (employeeId.HasValue)
        {
            query = query.Where(attendanceEvent =>
                attendanceEvent.EmployeeId == employeeId.Value);
        }

        return query
            .OrderBy(attendanceEvent => attendanceEvent.EmployeeId)
            .ThenBy(attendanceEvent => attendanceEvent.ServerTimestampUtc)
            .ThenBy(attendanceEvent => attendanceEvent.CreatedAtUtc)
            .ThenBy(attendanceEvent => attendanceEvent.Id);
    }

    public void Add(AttendanceEvent attendanceEvent)
    {
        dbContext.AttendanceEvents.Add(attendanceEvent);
    }

    public void Add(AttendanceCorrection attendanceCorrection)
    {
        dbContext.AttendanceCorrections.Add(attendanceCorrection);
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
