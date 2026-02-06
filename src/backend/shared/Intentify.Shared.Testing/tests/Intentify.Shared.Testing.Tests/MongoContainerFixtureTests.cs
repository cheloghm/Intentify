using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Intentify.Shared.Testing.Tests;

public sealed class MongoContainerFixtureTests : IAsyncLifetime
{
    private readonly MongoContainerFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void InitializeAsync_SetsNonEmptyConnectionString()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));
    }

    [Fact]
    public async Task CanConnectInsertAndQuery_WithSharedFixtureContainer()
    {
        var client = new MongoClient(_fixture.ConnectionString);
        var database = client.GetDatabase(_fixture.DatabaseName);
        var collection = database.GetCollection<TestDocument>("documents");

        var document = new TestDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Value = "fixture-round-trip"
        };

        await collection.InsertOneAsync(document);

        var loaded = await collection.Find(x => x.Id == document.Id).FirstOrDefaultAsync();

        Assert.NotNull(loaded);
        Assert.Equal(document.Value, loaded!.Value);
    }

    private sealed class TestDocument
    {
        [BsonId]
        public string Id { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
