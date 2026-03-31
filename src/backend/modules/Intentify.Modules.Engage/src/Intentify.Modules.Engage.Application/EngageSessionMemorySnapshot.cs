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
    bool IsConversationComplete)
{
    public static EngageSessionMemorySnapshot FromContext(EngageConversationContext context, EngageConversationPolicy policy)
    {
        var stage = string.IsNullOrWhiteSpace(context.Session.ConversationState)
            ? "Discover"
            : context.Session.ConversationState!;

        var discoveryFields = 0;
        if (!string.IsNullOrWhiteSpace(context.Session.CaptureGoal)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(context.Session.CaptureType)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(context.Session.CaptureLocation)) discoveryFields++;
        if (!string.IsNullOrWhiteSpace(context.Session.CaptureConstraints)) discoveryFields++;

        var explicitContactRequest = policy.IsExplicitCommercialContactRequest(context.UserMessage);
        var leadReady = policy.IsCommercialCaptureReady(context.Session, explicitContactRequest);
        var pendingMode = context.Session.PendingCaptureMode ?? string.Empty;

        return new EngageSessionMemorySnapshot(
            ActiveStage: stage,
            LastAssistantQuestion: context.LastAssistantQuestion,
            LastAssistantAskType: context.Session.LastAssistantAskType,
            Goal: context.Session.CaptureGoal,
            Type: context.Session.CaptureType,
            Location: context.Session.CaptureLocation,
            Constraints: context.Session.CaptureConstraints,
            Name: context.Session.CapturedName,
            PreferredContactMethod: context.Session.CapturedPreferredContactMethod,
            Email: context.Session.CapturedEmail,
            Phone: context.Session.CapturedPhone,
            DiscoveryFieldCount: discoveryFields,
            LeadReady: leadReady,
            IsSupportCaptureActive: string.Equals(pendingMode, "Support", StringComparison.OrdinalIgnoreCase),
            IsCommercialCaptureActive: string.Equals(pendingMode, "Commercial", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(stage, "CaptureLead", StringComparison.Ordinal),
            IsConversationComplete: context.Session.IsConversationComplete);
    }
}
