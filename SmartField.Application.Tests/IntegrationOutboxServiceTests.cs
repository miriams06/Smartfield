using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Domain.Enums;
using DomainIntegrationOutbox = SmartField.Domain.Entities.IntegrationOutbox;

namespace SmartField.Application.Tests;

public class IntegrationOutboxServiceTests
{
    [Fact]
    public void Add_CreatesPendingOutboxItem()
    {
        var store = new FakeIntegrationOutboxStore();
        var service = new IntegrationOutboxService(store);
        var companyId = Guid.Parse("f193cdd4-8760-4c24-b4e1-cd490d062f57");
        var entityId = Guid.Parse("f6d53720-e3c4-4c5c-b1c5-894a1a42472a");
        var createdAtUtc = new DateTimeOffset(
            2026,
            9,
            2,
            10,
            30,
            0,
            TimeSpan.FromHours(1));

        service.Add(new IntegrationOutboxMessage(
            companyId,
            " EmployeeCreated ",
            " Employee ",
            entityId,
            "{\"id\":\"f6d53720-e3c4-4c5c-b1c5-894a1a42472a\"}",
            createdAtUtc));

        var outbox = Assert.Single(store.Items);
        Assert.NotEqual(Guid.Empty, outbox.Id);
        Assert.Equal(companyId, outbox.CompanyId);
        Assert.Equal("EmployeeCreated", outbox.EventType);
        Assert.Equal("Employee", outbox.EntityType);
        Assert.Equal(entityId, outbox.EntityId);
        Assert.Equal(
            "{\"id\":\"f6d53720-e3c4-4c5c-b1c5-894a1a42472a\"}",
            outbox.Payload);
        Assert.Equal(IntegrationStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.AttemptCount);
        Assert.Equal(TimeSpan.Zero, outbox.CreatedAtUtc.Offset);
        Assert.Equal(createdAtUtc.ToUniversalTime(), outbox.CreatedAtUtc);
    }

    [Fact]
    public void Add_RejectsInvalidMessage()
    {
        var service = new IntegrationOutboxService(new FakeIntegrationOutboxStore());

        Assert.Throws<ArgumentException>(() =>
            service.Add(new IntegrationOutboxMessage(
                Guid.Empty,
                "EmployeeCreated",
                "Employee",
                Guid.NewGuid(),
                "{}",
                DateTimeOffset.UtcNow)));
    }

    private sealed class FakeIntegrationOutboxStore : IIntegrationOutboxStore
    {
        public List<DomainIntegrationOutbox> Items { get; } = [];

        public void Add(DomainIntegrationOutbox integrationOutbox)
        {
            Items.Add(integrationOutbox);
        }
    }
}
