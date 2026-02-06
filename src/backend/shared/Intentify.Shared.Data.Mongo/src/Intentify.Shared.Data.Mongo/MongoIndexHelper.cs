using MongoDB.Driver;

namespace Intentify.Shared.Data.Mongo;

public static class MongoIndexHelper
{
    public static Task EnsureIndexesAsync<T>(IMongoCollection<T> collection, IEnumerable<CreateIndexModel<T>> indexes, CancellationToken cancellationToken = default)
    {
        return collection.Indexes.CreateManyAsync(indexes, cancellationToken: cancellationToken);
    }
}
