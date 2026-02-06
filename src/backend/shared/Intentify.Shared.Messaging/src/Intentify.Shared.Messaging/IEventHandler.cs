namespace Intentify.Shared.Messaging;

public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent evt, CancellationToken cancellationToken = default);
}
