using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

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

        // Ensure GUIDs serialize with a defined representation (fixes GuidRepresentation.Unspecified errors)
        // Note: Some MongoDB.Bson versions don't have IsSerializerRegistered, so we register defensively.
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (ArgumentException)
        {
            // Serializer already registered - ignore
        }

        try
        {
            BsonSerializer.RegisterSerializer(
                new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));
        }
        catch (ArgumentException)
        {
            // Serializer already registered - ignore
        }

        _isRegistered = true;
    }
}
