namespace SmartField.Application.IntegrationOutbox;

public sealed record IntegrationOutboxMessage(
    Guid CompanyId,
    string EventType,
    string EntityType,
    Guid EntityId,
    string Payload,
    DateTimeOffset CreatedAtUtc);
