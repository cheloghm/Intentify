namespace Intentify.Shared.Data.Mongo;

public sealed class MongoOptions
{
    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;
}
