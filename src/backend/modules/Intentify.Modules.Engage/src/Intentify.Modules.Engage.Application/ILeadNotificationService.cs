namespace Intentify.Modules.Engage.Application;

/// <summary>
/// Sends a real-time "hot lead" email notification when a lead is captured
/// through the Engage chat widget.
/// Implemented in Engage.Api to keep email infrastructure out of the
/// application layer.
/// </summary>
public interface ILeadNotificationService
{
    /// <summary>
    /// Fire-and-forget: resolves recipients from bot config and sends the alert.
    /// Must not throw — implementations must swallow all exceptions internally.
    /// </summary>
    void NotifyHotLead(
        Guid tenantId,
        Guid siteId,
        string? capturedName,
        string? capturedEmail,
        string? userMessage,
        int?    intentScore,
        string? opportunityLabel);
}
