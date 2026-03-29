using Intentify.Modules.Engage.Domain;
using Intentify.Modules.Sites.Application;
using Intentify.Shared.Validation;
using Microsoft.Extensions.Logging;

namespace Intentify.Modules.Engage.Application;

public sealed class ChatSendHandler
{
    private readonly EngageOrchestrator _orchestrator;
    private readonly ILogger<ChatSendHandler> _logger;

    public ChatSendHandler(EngageOrchestrator orchestrator, ILogger<ChatSendHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<OperationResult<ChatSendResult>> HandleAsync(ChatSendCommand command, CancellationToken cancellationToken = default)
    {
        var validationErrors = new ValidationErrors();
        if (string.IsNullOrWhiteSpace(command.WidgetKey))
            validationErrors.Add("widgetKey", "Widget key is required.");
        if (string.IsNullOrWhiteSpace(command.Message))
            validationErrors.Add("message", "Message is required.");

        if (validationErrors.HasErrors)
            return OperationResult<ChatSendResult>.ValidationFailed(validationErrors);

        _logger.LogInformation("Engage chat send received for widgetKey {WidgetKey}", command.WidgetKey);

        return await _orchestrator.HandleAsync(command, cancellationToken);
    }
}
