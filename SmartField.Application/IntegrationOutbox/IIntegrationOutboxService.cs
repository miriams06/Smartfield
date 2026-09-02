namespace SmartField.Application.IntegrationOutbox;

public interface IIntegrationOutboxService
{
    void Add(IntegrationOutboxMessage message);
}
