using Intentify.Modules.Engage.Domain;
using Intentify.Shared.Validation;   // OperationResult<T> and ChatSendResult live here

namespace Intentify.Modules.Engage.Application;

public sealed class EngageStateRouter
{
    private readonly Dictionary<string, IEngageState> _states;

    public EngageStateRouter(IEnumerable<IEngageState> states)
    {
        _states = states.ToDictionary(s => s.StateName, s => s);
    }

    public async Task<OperationResult<ChatSendResult>> RouteAndHandleAsync(EngageConversationContext context, CancellationToken ct)
    {
        if (!_states.TryGetValue(context.RecommendedState, out var state))
        {
            // Fallback to Discover if state not found
            state = _states["Discover"];
        }

        return await state.HandleAsync(context, ct);
    }
}

public interface IEngageState
{
    string StateName { get; }
    Task<OperationResult<ChatSendResult>> HandleAsync(EngageConversationContext context, CancellationToken ct);
}
