using Intentify.Modules.Visitors.Domain;

namespace Intentify.Modules.Visitors.Application;

public static class VisitorIntentScorer
{
    public static int ComputeScore(Visitor visitor)
    {
        var score = 0;
        var sessions = visitor.Sessions ?? [];

        // Return visit signals
        var sessionCount = sessions.Count;
        if (sessionCount >= 1) score += 10;
        if (sessionCount >= 2) score += 10;
        if (sessionCount >= 3) score += 10;

        // Page depth
        var totalPages = sessions.Sum(s => s.PageViewCount > 0 ? s.PageViewCount : s.PagesVisited);
        if (totalPages >= 2)  score += 5;
        if (totalPages >= 5)  score += 10;
        if (totalPages >= 10) score += 10;

        // Time on site
        var totalTime = sessions.Sum(s => s.TotalTimeOnPageSeconds);
        if (totalTime >= 30)  score += 5;
        if (totalTime >= 120) score += 10;
        if (totalTime >= 300) score += 10;

        // Scroll depth (engagement)
        var maxScroll = sessions.Count > 0
            ? sessions.Max(s => s.MaxScrollDepthPct)
            : 0;
        if (maxScroll >= 50) score += 5;
        if (maxScroll >= 80) score += 10;

        // Chat engagement
        var chatCount = sessions.Sum(s => s.ChatEngagementCount);
        if (chatCount >= 1) score += 10;
        if (chatCount >= 3) score += 10;

        // Product / page interest
        if (!string.IsNullOrWhiteSpace(visitor.LastProductViewed)) score += 10;
        if (visitor.RecentProductViews?.Count >= 3)                 score += 5;

        // Identity signals
        if (!string.IsNullOrWhiteSpace(visitor.PrimaryEmail)) score += 10;
        if (!string.IsNullOrWhiteSpace(visitor.DisplayName))  score += 5;

        return Math.Min(score, 100);
    }
}
