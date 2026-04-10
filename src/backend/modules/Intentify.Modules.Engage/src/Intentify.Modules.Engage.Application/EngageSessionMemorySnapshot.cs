using Intentify.Modules.Engage.Domain;

namespace Intentify.Modules.Engage.Application;

public sealed record EngageSessionMemorySnapshot(
    string ActiveStage,
    string? LastAssistantQuestion,
    string? LastAssistantAskType,
    string? Goal,
    string? Type,
    string? Location,
    string? Constraints,
    string? Name,
    string? PreferredContactMethod,
    string? Email,
    string? Phone,
    int DiscoveryFieldCount,
    bool LeadReady,
    bool IsSupportCaptureActive,
    bool IsCommercialCaptureActive,
    bool IsConversationComplete,
    string? SurveyAnswer = null)
{
    /// <summary>
    /// Builds a session memory snapshot directly from the session entity.
    /// No policy dependency — the AI now decides lead readiness via the createLead flag.
    /// </summary>
    public static EngageSessionMemorySnapshot FromSession(EngageChatSession session, string? lastAssistantQuestion)
    {
        var stage = string.IsNullOrWhiteSpace(session.ConversationState) ? "Discover" : session.ConversationState;

        var discoveryFields = 0;
        if (!string.IsNullOrWhiteSpace(session.CaptureGoal)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureType)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureLocation)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(session.CaptureConstraints)) discoveryFields++;

        return new EngageSessionMemorySnapshot(
            ActiveStage: stage,
            LastAssistantQuestion: lastAssistantQuestion,
            LastAssistantAskType: session.LastAssistantAskType,
            Goal: session.CaptureGoal,
            Type: session.CaptureType,
            Location: session.CaptureLocation,
            Constraints: session.CaptureConstraints,
            Name: session.CapturedName,
            PreferredContactMethod: session.CapturedPreferredContactMethod,
            Email: session.CapturedEmail,
            Phone: session.CapturedPhone,
            DiscoveryFieldCount: discoveryFields,
            LeadReady: false,   // AI decides via createLead; this field is kept for structural compatibility
            IsSupportCaptureActive: string.Equals(session.PendingCaptureMode, "Support", StringComparison.OrdinalIgnoreCase),
            IsCommercialCaptureActive: string.Equals(session.PendingCaptureMode, "Commercial", StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(stage, "CaptureLead", StringComparison.Ordinal),
            IsConversationComplete: session.IsConversationComplete,
            SurveyAnswer: session.SurveyAnswer);
    }
}
