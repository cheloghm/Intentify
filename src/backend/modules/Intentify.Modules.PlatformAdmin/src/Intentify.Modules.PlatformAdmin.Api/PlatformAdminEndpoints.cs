using Intentify.Modules.PlatformAdmin.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;

namespace Intentify.Modules.PlatformAdmin.Api;

internal static class PlatformAdminEndpoints
{
    public static async Task<IResult> GetSummaryAsync(GetPlatformSummaryHandler handler, HttpContext context)
    {
        var result = await handler.HandleAsync(context.RequestAborted);
        return Results.Ok(ToSummaryResponse(result));
    }

    public static async Task<IResult> ListTenantsAsync(
        int page,
        int pageSize,
        string? search,
        ListPlatformTenantsHandler handler,
        HttpContext context)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 100 ? 25 : pageSize;

        var result = await handler.HandleAsync(new ListPlatformTenantsQuery(page, pageSize, search), context.RequestAborted);
        return Results.Ok(ToTenantListResponse(result));
    }

    public static async Task<IResult> GetTenantDetailAsync(string tenantId, GetPlatformTenantDetailHandler handler, HttpContext context)
    {
        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["tenantId"] = ["Tenant id is invalid."]
            }));
        }

        var result = await handler.HandleAsync(parsedTenantId, context.RequestAborted);
        if (result is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToTenantDetailResponse(result));
    }

    public static async Task<IResult> GetOperationalSummaryAsync(GetPlatformOperationalSummaryHandler handler, HttpContext context)
    {
        var result = await handler.HandleAsync(context.RequestAborted);
        return Results.Ok(new PlatformOperationalSummaryResponse(
            result.HealthStatus,
            result.TotalKnowledgeSources,
            result.IndexedKnowledgeSources,
            result.FailedKnowledgeSources,
            result.QueuedKnowledgeSources,
            result.ProcessingKnowledgeSources,
            result.OpenSearchEnabled,
            result.OpenSearchConfigured,
            result.GeneratedAtUtc));
    }

    private static PlatformSummaryResponse ToSummaryResponse(PlatformSummaryResult result)
        => new(
            result.TotalTenants,
            result.TotalSites,
            result.TotalVisitors,
            result.TotalEngageSessions,
            result.TotalEngageMessages,
            result.TotalTickets,
            result.TotalPromos,
            result.TotalPromoEntries,
            result.TotalIntelligenceTrendRecords,
            result.TotalKnowledgeSources,
            result.IndexedKnowledgeSources,
            result.FailedKnowledgeSources,
            result.HealthStatus,
            result.GeneratedAtUtc);

    private static PlatformTenantListResponse ToTenantListResponse(PlatformTenantListResult result)
        => new(
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.Items.Select(ToTenantListRowResponse).ToArray());

    private static PlatformTenantListRowResponse ToTenantListRowResponse(PlatformTenantListRowResult result)
        => new(
            result.TenantId.ToString("N"),
            result.TenantName,
            result.Domain,
            result.Plan,
            result.Industry,
            result.Category,
            result.CreatedAt,
            result.UpdatedAt,
            ToUsageResponse(result.Usage));

    private static PlatformTenantDetailResponse ToTenantDetailResponse(PlatformTenantDetailResult result)
        => new(
            result.TenantId.ToString("N"),
            result.TenantName,
            result.Domain,
            result.Plan,
            result.Industry,
            result.Category,
            result.CreatedAt,
            result.UpdatedAt,
            ToUsageResponse(result.Usage),
            new PlatformTenantRecentActivityResponse(
                result.RecentActivity.LastSiteActivityAtUtc,
                result.RecentActivity.LastVisitorActivityAtUtc,
                result.RecentActivity.LastEngageSessionActivityAtUtc,
                result.RecentActivity.LastTicketActivityAtUtc,
                result.RecentActivity.LastPromoActivityAtUtc,
                result.RecentActivity.LastPromoEntryActivityAtUtc,
                result.RecentActivity.LastIntelligenceActivityAtUtc,
                result.RecentActivity.LastAdsActivityAtUtc,
                result.RecentActivity.LastKnowledgeActivityAtUtc),
            result.Sites.Select(item => new PlatformTenantSiteResponse(
                item.SiteId.ToString("N"),
                item.Domain,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.FirstEventReceivedAtUtc)).ToArray());

    public static async Task<IResult> GetDashboardAsync(GetPlatformDashboardHandler handler, HttpContext context)
    {
        var result = await handler.HandleAsync(context.RequestAborted);
        return Results.Ok(new PlatformDashboardResponse(
            result.TotalTenants,
            result.TenantsThisWeek,
            result.TenantsThisMonth,
            result.TotalSites,
            result.ActiveSitesThisWeek,
            result.HealthySites,
            result.TotalVisitors,
            result.TotalLeads,
            result.TotalConversations,
            new PlanBreakdownResponse(result.PlanBreakdown.Starter, result.PlanBreakdown.Growth, result.PlanBreakdown.Agency, result.PlanBreakdown.Other),
            result.RecentSignups.Select(s => new RecentSignupResponse(s.TenantId, s.Name, s.Email, s.Plan, s.CreatedAt)).ToArray()));
    }

    // ── Feedback endpoints ───────────────────────────────────────────────────────

    public static IResult SubmitFeedbackAsync(SubmitFeedbackRequest request, FeedbackStore store, HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(ProblemDetailsHelpers.CreateValidationProblemDetails(
                new Dictionary<string, string[]> { ["title"] = ["Title is required."] }));
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        var item = store.Add(new FeedbackSubmission(
            Id: Guid.NewGuid().ToString(),
            Type: request.Type ?? "general",
            Title: request.Title.Trim(),
            Description: request.Description?.Trim(),
            Priority: request.Priority,
            SubmittedAt: DateTime.UtcNow.ToString("o"),
            Status: "pending",
            SubmittedByUserId: userId));

        return Results.Ok(item);
    }

    public static IResult ListFeedbackAsync(FeedbackStore store)
        => Results.Ok(store.GetAll());

    public static IResult UpdateFeedbackStatusAsync(string id, UpdateFeedbackStatusRequest request, FeedbackStore store)
    {
        var updated = store.UpdateStatus(id, request.Status ?? "pending");
        return updated is null ? Results.NotFound() : Results.Ok(updated);
    }

    private static PlatformTenantUsageResponse ToUsageResponse(PlatformTenantUsageResult usage)
        => new(
            usage.SiteCount,
            usage.VisitorsCount,
            usage.EngageSessionsCount,
            usage.EngageMessagesCount,
            usage.TicketsCount,
            usage.PromosCount,
            usage.PromoEntriesCount,
            usage.IntelligenceRecordCount,
            usage.AdsCampaignCount,
            usage.KnowledgeSourcesCount,
            usage.KnowledgeIndexedCount,
            usage.KnowledgeFailedCount,
            usage.LastActivityAtUtc);
}
