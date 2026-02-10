using Xunit;

namespace Intentify.Shared.KeyManagement.Tests;

public sealed class KeyGeneratorTests
{
    [Fact]
    public void GenerateKey_ReturnsNonEmptyUrlSafeValue()
    {
        var generator = new KeyGenerator();

        var key = generator.GenerateKey(KeyPurpose.SiteKey);

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.DoesNotContain('=', key);
        Assert.DoesNotContain('+', key);
        Assert.DoesNotContain('/', key);
    }

    [Fact]
    public void GenerateKey_ProducesUniqueValues()
    {
        var generator = new KeyGenerator();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 1000; i++)
        {
            var key = generator.GenerateKey(KeyPurpose.WidgetKey);
            Assert.True(keys.Add(key), "Expected unique key value.");
        }
    }
}
