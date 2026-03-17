using Intentify.Modules.Flows.Application;
using Intentify.Modules.Flows.Domain;

namespace Intentify.Modules.Flows.Tests;

public sealed class ExecuteFlowsForTriggerServiceTests
{
    [Fact]
    public async Task HandleAsync_MatchesByTriggerAndFilters()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var flow = new FlowDefinition
        {
            TenantId = tenantId,
            SiteId = siteId,
            Name = "Flow",
            Enabled = true,
            Trigger = new FlowTrigger
            {
                TriggerType = "IntelligenceTrendsUpdated",
                Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["window"] = "7d"
                }
            },
            Conditions = [],
            Actions = [new FlowAction { ActionType = "LogRun" }]
        };

        var flowsRepo = new InMemoryFlowsRepository([flow]);
        var runsRepo = new InMemoryRunsRepository();
        var service = new ExecuteFlowsForTriggerService(flowsRepo, runsRepo);

        var result = await service.HandleAsync(new ExecuteFlowsTriggerCommand(
            tenantId,
            siteId,
            "IntelligenceTrendsUpdated",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["window"] = "7d" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["category"] = "Marketing" }));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.MatchedFlows);
        Assert.Single(runsRepo.Items);
    }

    private sealed class InMemoryFlowsRepository(IReadOnlyCollection<FlowDefinition> items) : IFlowsRepository
    {
        public Task InsertAsync(FlowDefinition flow, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlowDefinition?> GetAsync(Guid tenantId, Guid flowId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlowDefinition?> ReplaceAsync(FlowDefinition flow, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FlowDefinition>> ListBySiteAsync(Guid tenantId, Guid siteId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyCollection<FlowDefinition>)items.Where(x => x.TenantId == tenantId && x.SiteId == siteId).ToArray());
    }

    private sealed class InMemoryRunsRepository : IFlowRunsRepository
    {
        public List<FlowRun> Items { get; } = [];
        public Task InsertAsync(FlowRun run, CancellationToken ct = default)
        {
            Items.Add(run);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<FlowRun>> ListByFlowAsync(Guid tenantId, Guid flowId, int limit, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyCollection<FlowRun>)Items.Where(x => x.TenantId == tenantId && x.FlowId == flowId).Take(limit).ToArray());
    }
}
