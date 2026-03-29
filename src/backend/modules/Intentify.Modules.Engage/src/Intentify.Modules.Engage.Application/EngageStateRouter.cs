using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;   // for OperationResult<T>

public sealed class EngageStateRouter
{
    private readonly Dictionary<string, IEngageState> _states;

    public EngageStateRouter(IEnumerable<IEngageState> states)
    {
        _states = states.ToDictionary(s => s.StateName, s => s);
    }

    public async Task<OperationResult<ChatSendResult>> RouteAndHandleAsync(EngageConversationContext context, CancellationToken ct)
    {
        var state = _states[context.RecommendedState];
        return await state.HandleAsync(context, ct);
    }
}

public interface IEngageState
{
    string StateName { get; }
    Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext context, CancellationToken ct);
}
