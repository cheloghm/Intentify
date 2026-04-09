namespace Intentify.Modules.Intelligence.Application;

public interface ICompetitorSignalsService
{
    Task<CompetitorSignalsResult> GetCompetitorSignalsAsync(
        CompetitorSignalsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record CompetitorSignalsQuery(
    string Industry,
    string? Location = "GB",
    string? TimeWindow = "7d",
    int MaxResults = 10);

public sealed record CompetitorSignalsResult(
    IReadOnlyList<CompetitorKeywordSignal> TrendingKeywords,
    IReadOnlyList<CompetitorKeywordSignal> RisingKeywords,
    string? AiSummary,
    DateTime GeneratedAtUtc,
    bool IsFromCache);

public sealed record CompetitorKeywordSignal(
    string Keyword,
    int Score,
    string? Intent,   // "Transactional" | "Commercial" | "Informational"
    bool IsRising);
