public sealed class ChatSendHandler
{
    private readonly EngageOrchestrator _orchestrator;

    public ChatSendHandler(EngageOrchestrator orchestrator) => _orchestrator = orchestrator;

    public async Task<OperationResult<ChatSendResult>> HandleAsync(ChatSendCommand command, CancellationToken cancellationToken)
        => await _orchestrator.HandleAsync(command, cancellationToken);
}
