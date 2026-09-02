using SmartField.Application.Abstractions;
using SmartField.Application.Audit;
using SmartField.Domain.Entities;

namespace SmartField.Application.Tests;

public class AuditServiceTests
{
    private static readonly Guid CompanyId = Guid.Parse("2e4c221f-7e60-44a8-bf6c-82b5ca43d71f");
    private static readonly Guid OtherCompanyId = Guid.Parse("a2a6cbbc-4706-43c6-a9b7-2d24bd901bed");
    private static readonly Guid UserId = Guid.Parse("083d9c50-df30-4478-a8aa-a0b3e6868aec");

    [Fact]
    public void Add_CreatesAuditLogWithOldAndNewValues()
    {
        var store = new FakeAuditStore();
        var service = new AuditService(store, new FakeCurrentCompanyProvider(CompanyId));
        var entityId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 9, 2, 14, 30, 0, TimeSpan.FromHours(1));

        service.Add(
            CompanyId,
            UserId,
            "Employee",
            entityId,
            "Updated",
            "{\"name\":\"Antes\"}",
            "{\"name\":\"Depois\"}",
            timestamp);

        var audit = Assert.Single(store.Added);
        Assert.Equal(CompanyId, audit.CompanyId);
        Assert.Equal(UserId, audit.UserId);
        Assert.Equal("Employee", audit.EntityType);
        Assert.Equal(entityId, audit.EntityId);
        Assert.Equal("Updated", audit.Action);
        Assert.Equal("{\"name\":\"Antes\"}", audit.OldValues);
        Assert.Equal("{\"name\":\"Depois\"}", audit.NewValues);
        Assert.Equal(timestamp.ToUniversalTime(), audit.TimestampUtc);
        Assert.Equal(timestamp.ToUniversalTime(), audit.CreatedAtUtc);
    }

    [Fact]
    public async Task GetAsync_ReturnsOnlyCurrentCompanyAudit()
    {
        var store = new FakeAuditStore();
        store.Items.AddRange(
        [
            new AuditLogDto(Guid.NewGuid(), CompanyId, UserId, "admin@smartfield.local", "Employee", Guid.NewGuid(), "Created", null, "{}", DateTimeOffset.UtcNow),
            new AuditLogDto(Guid.NewGuid(), OtherCompanyId, UserId, "other@smartfield.local", "Employee", Guid.NewGuid(), "Created", null, "{}", DateTimeOffset.UtcNow)
        ]);
        var service = new AuditService(store, new FakeCurrentCompanyProvider(CompanyId));

        var result = await service.GetAsync(CancellationToken.None);

        var audit = Assert.Single(result);
        Assert.Equal(CompanyId, audit.CompanyId);
        Assert.Equal("admin@smartfield.local", audit.UserEmail);
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyWithoutCurrentCompany()
    {
        var service = new AuditService(
            new FakeAuditStore(),
            new FakeCurrentCompanyProvider(null));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    private sealed class FakeCurrentCompanyProvider : ICurrentCompanyProvider
    {
        public FakeCurrentCompanyProvider(Guid? companyId)
        {
            CompanyId = companyId;
        }

        public Guid? CompanyId { get; }
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public List<AuditLog> Added { get; } = [];
        public List<AuditLogDto> Items { get; } = [];

        public Task<IReadOnlyList<AuditLogDto>> GetAsync(
            Guid companyId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<AuditLogDto> result = Items
                .Where(item => item.CompanyId == companyId)
                .OrderByDescending(item => item.TimestampUtc)
                .ToArray();
            return Task.FromResult(result);
        }

        public void Add(AuditLog auditLog) => Added.Add(auditLog);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
