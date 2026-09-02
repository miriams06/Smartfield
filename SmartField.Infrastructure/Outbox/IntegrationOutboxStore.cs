using SmartField.Application.IntegrationOutbox;
using SmartField.Domain.Entities;
using SmartField.Infrastructure.Persistence;

namespace SmartField.Infrastructure.Outbox;

public sealed class IntegrationOutboxStore : IIntegrationOutboxStore
{
    private readonly SmartFieldDbContext dbContext;

    public IntegrationOutboxStore(SmartFieldDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public void Add(IntegrationOutbox integrationOutbox)
    {
        dbContext.IntegrationOutbox.Add(integrationOutbox);
    }
}
