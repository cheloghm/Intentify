using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Intentify.Shared.Security.Tests;

public sealed class SecurityTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "intentify",
        Audience = "intentify-users",
        SigningKey = "very_secret_signing_key_for_tests_only_12345",
        AccessTokenMinutes = 5
    };

    [Fact]
    public void IssueToken_ThenValidate_Succeeds()
    {
        var issuer = new JwtTokenIssuer();
        var validator = new JwtTokenValidator();

        var issueResult = issuer.IssueAccessToken("user-1", "tenant-1", ["admin"], Options);
        Assert.True(issueResult.IsSuccess);

        var validateResult = validator.Validate(issueResult.Value!, Options);
        Assert.True(validateResult.IsSuccess);
    }

    [Fact]
    public void IssuedToken_ContainsTenantIdClaim()
    {
        var issuer = new JwtTokenIssuer();
        var issueResult = issuer.IssueAccessToken("user-1", "tenant-42", ["agent"], Options);

        Assert.True(issueResult.IsSuccess);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(issueResult.Value!);
        var tenantClaim = token.Claims.Single(c => c.Type == "tenantId");
        Assert.Equal("tenant-42", tenantClaim.Value);
    }

    [Fact]
    public void HashPassword_ThenVerify_ReturnsTrue()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("my-password");

        Assert.True(hasher.VerifyPassword("my-password", hash));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("my-password");

        Assert.False(hasher.VerifyPassword("wrong-password", hash));
    }
}
