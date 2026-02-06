using Intentify.Shared.Abstractions;
using MongoDB.Driver;

namespace Intentify.Shared.Data.Mongo;

public static class MongoClientFactory
{
    public static Result<MongoClient> CreateMongoClient(MongoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return Result<MongoClient>.Failure(new Error("MongoOptions.ConnectionStringMissing", "ConnectionString is required."));
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            return Result<MongoClient>.Failure(new Error("MongoOptions.DatabaseNameMissing", "DatabaseName is required."));
        }

        return Result<MongoClient>.Success(new MongoClient(options.ConnectionString));
    }
}
