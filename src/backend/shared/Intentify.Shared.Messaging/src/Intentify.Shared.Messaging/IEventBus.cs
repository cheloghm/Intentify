namespace Intentify.Shared.Messaging;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent evt) where TEvent : IEvent;
}
