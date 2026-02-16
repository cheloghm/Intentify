namespace Intentify.Modules.Engage.Domain;

public sealed class EngageBot
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid BotId { get; init; } = Guid.NewGuid();

    public Guid TenantId { get; init; }

    public Guid SiteId { get; init; }
}
