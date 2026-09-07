using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartField.Application.Attendance;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Attendance;

public sealed class SqlServerAttendancePunchConcurrencyGate : IAttendancePunchConcurrencyGate
{
    private readonly SmartFieldDbContext dbContext;

    public SqlServerAttendancePunchConcurrencyGate(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<T> ExecuteAsync<T>(
        Guid companyId,
        Guid employeeId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        await dbContext.Database.SqlQuery<Guid>($"""
            SELECT [Id] AS [Value]
            FROM [Employees] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [CompanyId] = {companyId}
              AND [Id] = {employeeId}
            """)
            .SingleOrDefaultAsync(cancellationToken);

        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
