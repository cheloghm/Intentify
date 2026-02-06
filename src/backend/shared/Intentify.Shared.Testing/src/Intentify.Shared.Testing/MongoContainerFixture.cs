using Testcontainers.MongoDb;

namespace Intentify.Shared.Testing;

public sealed class MongoContainerFixture : IAsyncDisposable
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder().Build();
    private bool _started;

    public string ConnectionString { get; private set; } = string.Empty;

    public string DatabaseName { get; } = $"intentify-test-{Guid.NewGuid():N}";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await _mongoContainer.StartAsync(cancellationToken);
        ConnectionString = _mongoContainer.GetConnectionString();
        _started = true;
    }

    public async ValueTask DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }
}
