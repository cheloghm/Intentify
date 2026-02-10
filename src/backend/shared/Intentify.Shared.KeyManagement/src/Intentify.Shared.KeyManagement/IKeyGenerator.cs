namespace Intentify.Shared.KeyManagement;

public interface IKeyGenerator
{
    string GenerateKey(KeyPurpose purpose);
}
