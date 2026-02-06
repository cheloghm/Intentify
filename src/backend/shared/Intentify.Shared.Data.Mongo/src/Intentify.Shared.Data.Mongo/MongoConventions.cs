using MongoDB.Bson.Serialization.Conventions;

namespace Intentify.Shared.Data.Mongo;

public static class MongoConventions
{
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreIfNullConvention(true)
        };

        ConventionRegistry.Register("Intentify.Shared.Data.Mongo", conventionPack, _ => true);
        _isRegistered = true;
    }
}
