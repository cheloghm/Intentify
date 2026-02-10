using Intentify.Modules.Collector.Domain;

namespace Intentify.Modules.Collector.Application;

public interface ICollectorEventRepository
{
    Task InsertAsync(CollectorEvent collectorEvent, CancellationToken cancellationToken = default);
}
