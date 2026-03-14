using Intentify.Shared.AI;
using Intentify.Shared.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Intentify.Modules.Intelligence.Application;

public sealed record SiteInsightsSummaryResponse(
    Guid SiteId,
    string Category,
    string Location,
    string TimeWindow,
    string Summary,
    bool UsedAi,
    DateTime GeneratedAtUtc);

public sealed class GetSiteInsightsSummaryService(
    QueryIntelligenceTrendsService trendsService,
    IServiceProvider serviceProvider)
{
    public async Task<OperationResult<SiteInsightsSummaryResponse>> HandleAsync(
        string tenantId,
        IntelligenceDashboardQuery query,
        CancellationToken ct = default)
    {
        var dashboardResult = await trendsService.HandleDashboardAsync(tenantId, query, ct);
        if (dashboardResult.Status != OperationStatus.Success)
        {
            return dashboardResult.Status switch
            {
                OperationStatus.ValidationFailed => OperationResult<SiteInsightsSummaryResponse>.ValidationFailed(dashboardResult.Errors!),
                OperationStatus.NotFound => OperationResult<SiteInsightsSummaryResponse>.NotFound(),
                _ => OperationResult<SiteInsightsSummaryResponse>.Failure()
            };
        }

        var dashboard = dashboardResult.Value!;
        var fallbackSummary = BuildFallbackSummary(dashboard);

        var aiClient = serviceProvider.GetService<IChatCompletionClient>();
        if (aiClient is null)
        {
            return OperationResult<SiteInsightsSummaryResponse>.Success(new SiteInsightsSummaryResponse(
                dashboard.SiteId,
                dashboard.Category,
                dashboard.Location,
                dashboard.TimeWindow,
                fallbackSummary,
                UsedAi: false,
                DateTime.UtcNow));
        }

        var prompt = BuildPrompt(dashboard, fallbackSummary);
        var aiResult = await aiClient.CompleteAsync(prompt, ct);
        var aiSummary = aiResult.IsSuccess ? NormalizeSummary(aiResult.Value) : null;

        return OperationResult<SiteInsightsSummaryResponse>.Success(new SiteInsightsSummaryResponse(
            dashboard.SiteId,
            dashboard.Category,
            dashboard.Location,
            dashboard.TimeWindow,
            string.IsNullOrWhiteSpace(aiSummary) ? fallbackSummary : aiSummary,
            UsedAi: !string.IsNullOrWhiteSpace(aiSummary),
            DateTime.UtcNow));
    }

    private static string BuildFallbackSummary(IntelligenceDashboardResponse dashboard)
    {
        var topItems = dashboard.TopItems.Take(3).Select(item => item.QueryOrTopic).Where(static x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var topItemsText = topItems.Length == 0 ? "No top topics available yet" : string.Join(", ", topItems);

        return $"{dashboard.TotalItems} trend items for {dashboard.Category} in {dashboard.Location} over {dashboard.TimeWindow}. " +
               $"Top topics: {topItemsText}. " +
               $"Average score {dashboard.Summary.AverageScore:0.##}, max score {dashboard.Summary.MaxScore:0.##}.";
    }

    private static string BuildPrompt(IntelligenceDashboardResponse dashboard, string fallbackSummary)
    {
        var topItems = dashboard.TopItems.Take(5)
            .Select(item => $"- {item.QueryOrTopic} (score: {item.Score:0.##}, rank: {(item.Rank?.ToString() ?? "n/a")}, provider: {item.Provider})");

        return "You are an analytics assistant for a SaaS dashboard. " +
               "Write a concise 2-3 sentence summary grounded only in the provided data. " +
               "Do not speculate. Mention trend volume, strongest topics, and score signal.\n\n" +
               $"Fallback summary: {fallbackSummary}\n" +
               $"Site: {dashboard.SiteId}\n" +
               $"Category: {dashboard.Category}\n" +
               $"Location: {dashboard.Location}\n" +
               $"Time Window: {dashboard.TimeWindow}\n" +
               $"Total Items: {dashboard.TotalItems}\n" +
               $"Average Score: {dashboard.Summary.AverageScore:0.##}\n" +
               $"Max Score: {dashboard.Summary.MaxScore:0.##}\n" +
               $"Ranked Items: {dashboard.Summary.RankedItemsCount}\n" +
               "Top Items:\n" + string.Join("\n", topItems);
    }

    private static string? NormalizeSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > 600 ? normalized[..600] : normalized;
    }
}
