using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Intentify.Shared.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Intentify.Shared.Security;

public sealed class JwtTokenIssuer
{
    public Result<string> IssueAccessToken(string userId, string tenantId, IEnumerable<string> roles, JwtOptions options)
    {
        var optionsValidation = ValidateOptions(options);
        if (!optionsValidation.IsSuccess)
        {
            return Result<string>.Failure(optionsValidation.Error!);
        }

        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("tenantId", tenantId)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(options.AccessTokenMinutes),
            signingCredentials: credentials);

        return Result<string>.Success(new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static Result ValidateOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            return Result.Failure(new Error("JwtOptions.IssuerMissing", "Issuer is required."));
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            return Result.Failure(new Error("JwtOptions.AudienceMissing", "Audience is required."));
        }

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Result.Failure(new Error("JwtOptions.SigningKeyMissing", "SigningKey is required."));
        }

        if (options.AccessTokenMinutes <= 0)
        {
            return Result.Failure(new Error("JwtOptions.AccessTokenMinutesInvalid", "AccessTokenMinutes must be greater than 0."));
        }

        return Result.Success();
    }
}
