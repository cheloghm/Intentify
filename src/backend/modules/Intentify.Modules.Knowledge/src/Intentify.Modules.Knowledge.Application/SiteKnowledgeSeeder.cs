using Intentify.Modules.Knowledge.Domain;
using Intentify.Modules.Sites.Application;

namespace Intentify.Modules.Knowledge.Application;

/// <summary>
/// Implements ISiteKnowledgeSeeder to insert default knowledge sources
/// when a new site is created, without creating a circular project reference.
/// </summary>
public sealed class SiteKnowledgeSeeder : ISiteKnowledgeSeeder
{
    private readonly IKnowledgeSourceRepository _sources;

    public SiteKnowledgeSeeder(IKnowledgeSourceRepository sources)
    {
        _sources = sources;
    }

    public async Task SeedDefaultSourcesAsync(
        Guid tenantId,
        Guid siteId,
        string domain,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Source 1 — URL crawler for the site's homepage
        var urlSource = new KnowledgeSource
        {
            TenantId   = tenantId,
            SiteId     = siteId,
            BotId      = Guid.Empty,
            Type       = "Url",
            Name       = $"{domain} — Website",
            Url        = $"https://{domain}",
            Status     = IndexStatus.Queued,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        // Source 2 — Quick facts template for the user to fill in
        var quickFactsText =
            $"Company name: [Your company name]\n" +
            $"What we do: [One sentence description of your product or service]\n" +
            $"Key products / services: [List your main offerings]\n" +
            $"Target customers: [Who you sell to — industry, company size, role]\n" +
            $"Pricing: [Brief pricing summary or 'Contact us for pricing']\n" +
            $"Location: [City, Country]\n" +
            $"Website: https://{domain}\n" +
            $"Contact email: [hello@yourdomain.com]\n" +
            $"Unique selling point: [What makes you different from competitors]";

        var textSource = new KnowledgeSource
        {
            TenantId    = tenantId,
            SiteId      = siteId,
            BotId       = Guid.Empty,
            Type        = "Text",
            Name        = "Quick Facts — Edit this",
            TextContent = quickFactsText,
            Status      = IndexStatus.Queued,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _sources.InsertSourceAsync(urlSource, cancellationToken);
        await _sources.InsertSourceAsync(textSource, cancellationToken);
    }
}
