using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Intentify.Shared.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Intentify.Shared.Security;

public sealed class JwtTokenValidator
{
    public Result<ClaimsPrincipal> Validate(string token, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<ClaimsPrincipal>.Failure(new Error("Jwt.TokenMissing", "Token is required."));
        }

        var optionsValidation = ValidateOptions(options);
        if (!optionsValidation.IsSuccess)
        {
            return Result<ClaimsPrincipal>.Failure(optionsValidation.Error!);
        }

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = handler.ValidateToken(token, validationParameters, out _);
            return Result<ClaimsPrincipal>.Success(principal);
        }
        catch (Exception ex)
        {
            return Result<ClaimsPrincipal>.Failure(new Error("Jwt.InvalidToken", ex.Message));
        }
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

        return Result.Success();
    }
}
