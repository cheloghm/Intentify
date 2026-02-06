using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Shared.Messaging.Tests;

public sealed class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_InvokesRegisteredHandler()
    {
        var services = new ServiceCollection();
        var handler = new TestEventHandler();
        services.AddSingleton<IEventHandler<TestEvent>>(handler);
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new TestEvent());

        Assert.True(handler.Invoked);
    }

    private sealed record TestEvent : IEvent;

    private sealed class TestEventHandler : IEventHandler<TestEvent>
    {
        public bool Invoked { get; private set; }

        public Task HandleAsync(TestEvent evt, CancellationToken cancellationToken = default)
        {
            Invoked = true;
            return Task.CompletedTask;
        }
    }
}
