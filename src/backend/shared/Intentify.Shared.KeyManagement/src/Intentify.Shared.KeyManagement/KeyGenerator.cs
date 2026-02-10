using System.Security.Cryptography;

namespace Intentify.Shared.KeyManagement;

public sealed class KeyGenerator : IKeyGenerator
{
    private const int DefaultKeyBytes = 32;

    public string GenerateKey(KeyPurpose purpose)
    {
        var bytes = RandomNumberGenerator.GetBytes(DefaultKeyBytes);
        return ToBase64Url(bytes);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        var raw = Convert.ToBase64String(bytes);
        return raw.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
