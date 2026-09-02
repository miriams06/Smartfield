namespace SmartField.Application.IntegrationOutbox;

public interface IIntegrationOutboxStore
{
    void Add(SmartField.Domain.Entities.IntegrationOutbox integrationOutbox);
}
