using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Shared.Messaging;

public sealed class InMemoryEventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent evt) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(evt);

        var handlers = serviceProvider.GetServices<IEventHandler<TEvent>>();
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(evt);
        }
    }
}
