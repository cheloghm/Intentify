using Xunit;

namespace Intentify.Shared.Validation.Tests;

public sealed class NormalizationTests
{
    [Theory]
    [InlineData("https://app.intentify.local", "https://app.intentify.local")]
    [InlineData("http://localhost:3000", "http://localhost:3000")]
    public void OriginNormalizer_AcceptsValidOrigins(string value, string expected)
    {
        var result = OriginNormalizer.TryNormalize(value, out var normalized);

        Assert.True(result);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("example.com")]
    public void OriginNormalizer_RejectsInvalidOrigins(string value)
    {
        var result = OriginNormalizer.TryNormalize(value, out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Example.com", "example.com")]
    [InlineData(" localhost ", "localhost")]
    public void DomainNormalizer_NormalizesDomains(string value, string expected)
    {
        var result = DomainNormalizer.TryNormalize(value, out var normalized);

        Assert.True(result);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("bad domain.com")]
    public void DomainNormalizer_RejectsInvalidDomains(string value)
    {
        var result = DomainNormalizer.TryNormalize(value, out _);

        Assert.False(result);
    }
}
