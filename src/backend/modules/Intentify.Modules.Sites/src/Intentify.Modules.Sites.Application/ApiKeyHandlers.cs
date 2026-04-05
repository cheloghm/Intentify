using System.Security.Cryptography;
using System.Text;
using Intentify.Modules.Sites.Domain;
using Intentify.Shared.KeyManagement;
using Intentify.Shared.Validation;
using MongoDB.Driver;

namespace Intentify.Modules.Sites.Application;

// ── Contracts ─────────────────────────────────────────────────────────────────

public sealed record ApiKeyResult(
    string KeyId,
    string Label,
    string Hint,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    bool IsActive);

public sealed record GenerateApiKeyResult(
    string KeyId,
    string Label,
    string RawSecret,   // Returned ONCE. Never stored.
    string Hint,
    DateTime CreatedAtUtc);

// ── Generate handler ──────────────────────────────────────────────────────────

public sealed class GenerateApiKeyHandler(ISiteRepository sites, IKeyGenerator keyGenerator)
{
    public async Task<OperationResult<GenerateApiKeyResult>> HandleAsync(
        GenerateApiKeyCommand command, CancellationToken ct = default)
    {
        var errors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.Label))
            errors.Add("label", "Label is required.");
        if (command.Label?.Trim().Length > 64)
            errors.Add("label", "Label cannot exceed 64 characters.");
        if (errors.HasErrors) return OperationResult<GenerateApiKeyResult>.ValidationFailed(errors);

        var site = await sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, ct);
        if (site is null) return OperationResult<GenerateApiKeyResult>.NotFound();

        // Generate the raw secret with prefix so clients recognise it
        var rawRandom  = keyGenerator.GenerateKey(KeyPurpose.ApiKey);
        var rawSecret  = $"itfy_{rawRandom}";
        var secretHash = HashSecret(rawSecret);
        var hint       = rawSecret[..Math.Min(12, rawSecret.Length)] + "…";

        var apiKey = new SiteApiKey
        {
            KeyId      = Guid.NewGuid().ToString("N"),
            Label      = command.Label.Trim(),
            SecretHash = secretHash,
            Hint       = hint,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await sites.AddApiKeyAsync(command.TenantId, command.SiteId, apiKey, ct);

        return OperationResult<GenerateApiKeyResult>.Success(new GenerateApiKeyResult(
            apiKey.KeyId,
            apiKey.Label,
            rawSecret,   // ← shown once to the user, never stored
            apiKey.Hint,
            apiKey.CreatedAtUtc));
    }

    internal static string HashSecret(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ── List handler ──────────────────────────────────────────────────────────────

public sealed class ListApiKeysHandler(ISiteRepository sites)
{
    public async Task<OperationResult<IReadOnlyList<ApiKeyResult>>> HandleAsync(
        ListApiKeysCommand command, CancellationToken ct = default)
    {
        var site = await sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, ct);
        if (site is null) return OperationResult<IReadOnlyList<ApiKeyResult>>.NotFound();

        var keys = site.ApiKeys
            .Select(k => new ApiKeyResult(k.KeyId, k.Label, k.Hint, k.CreatedAtUtc, k.RevokedAtUtc, k.IsActive))
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToList();

        return OperationResult<IReadOnlyList<ApiKeyResult>>.Success(keys);
    }
}

// ── Revoke handler ────────────────────────────────────────────────────────────

public sealed class RevokeApiKeyHandler(ISiteRepository sites)
{
    public async Task<OperationResult<bool>> HandleAsync(
        RevokeApiKeyCommand command, CancellationToken ct = default)
    {
        var site = await sites.GetByTenantAndIdAsync(command.TenantId, command.SiteId, ct);
        if (site is null) return OperationResult<bool>.NotFound();

        var key = site.ApiKeys.FirstOrDefault(k => k.KeyId == command.KeyId);
        if (key is null)
        {
            var e = new ValidationErrors(); e.Add("keyId", "API key not found.");
            return OperationResult<bool>.ValidationFailed(e);
        }

        if (!key.IsActive) return OperationResult<bool>.Success(false);

        await sites.RevokeApiKeyAsync(command.TenantId, command.SiteId, command.KeyId, DateTime.UtcNow, ct);
        return OperationResult<bool>.Success(true);
    }
}
