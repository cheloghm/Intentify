using MongoDB.Driver;
using MongoDB.Bson.Serialization.Attributes;
using Testcontainers.MongoDb;

namespace Intentify.Shared.Data.Mongo.Tests;

public sealed class MongoIntegrationTests
{
    [Fact]
    public async Task MongoContainer_RoundTripInsertAndQuery_Succeeds()
    {
        var mongoDbContainer = new MongoDbBuilder().Build();
        await mongoDbContainer.StartAsync();

        try
        {
            var options = new MongoOptions
            {
                ConnectionString = mongoDbContainer.GetConnectionString(),
                DatabaseName = "intentify-test"
            };

            var clientResult = MongoClientFactory.CreateMongoClient(options);
            Assert.True(clientResult.IsSuccess);

            MongoConventions.Register();

            var database = clientResult.Value!.GetDatabase(options.DatabaseName);
            var collection = database.GetCollection<TestDocument>("documents");

            var document = new TestDocument { Id = Guid.NewGuid().ToString("N"), Value = "round-trip" };
            await collection.InsertOneAsync(document);

            var loaded = await collection.Find(x => x.Id == document.Id).FirstOrDefaultAsync();

            Assert.NotNull(loaded);
            Assert.Equal(document.Value, loaded!.Value);
        }
        finally
        {
            await mongoDbContainer.DisposeAsync();
        }
    }

    private sealed class TestDocument
    {
        [BsonId]
        public string Id { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;
    }
}
