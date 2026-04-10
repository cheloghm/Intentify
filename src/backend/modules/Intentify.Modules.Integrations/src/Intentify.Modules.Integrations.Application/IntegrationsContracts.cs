namespace Intentify.Modules.Integrations.Application;

public sealed record ListWebhooksQuery(Guid TenantId, Guid SiteId);

public sealed record CreateWebhookCommand(
    Guid TenantId,
    Guid SiteId,
    string Url,
    string Label,
    string Type,
    IReadOnlyCollection<string> Events);

public sealed record DeleteWebhookCommand(Guid TenantId, Guid Id);

public sealed record TestWebhookCommand(Guid TenantId, Guid Id);

public sealed record WebhookEndpointResult(
    Guid Id,
    Guid SiteId,
    string Url,
    string Label,
    string Type,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record WebhookDispatchPayload(
    string Event,
    Guid TenantId,
    Guid SiteId,
    DateTime OccurredAtUtc,
    Dictionary<string, object?> Data);
