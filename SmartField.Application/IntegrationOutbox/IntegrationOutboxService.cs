using SmartField.Domain.Enums;

namespace SmartField.Application.IntegrationOutbox;

public sealed class IntegrationOutboxService : IIntegrationOutboxService
{
    private readonly IIntegrationOutboxStore integrationOutboxStore;

    public IntegrationOutboxService(IIntegrationOutboxStore integrationOutboxStore)
    {
        this.integrationOutboxStore = integrationOutboxStore;
    }

    public void Add(IntegrationOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("A empresa é obrigatória.", nameof(message));
        }

        if (message.EntityId == Guid.Empty)
        {
            throw new ArgumentException("A entidade é obrigatória.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.EventType))
        {
            throw new ArgumentException("O tipo de evento é obrigatório.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.EntityType))
        {
            throw new ArgumentException("O tipo de entidade é obrigatório.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Payload))
        {
            throw new ArgumentException("O payload é obrigatório.", nameof(message));
        }

        integrationOutboxStore.Add(new SmartField.Domain.Entities.IntegrationOutbox
        {
            Id = Guid.NewGuid(),
            CompanyId = message.CompanyId,
            EventType = message.EventType.Trim(),
            EntityType = message.EntityType.Trim(),
            EntityId = message.EntityId,
            Payload = message.Payload,
            Status = IntegrationStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = message.CreatedAtUtc.ToUniversalTime()
        });
    }
}
