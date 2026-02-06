using Microsoft.Extensions.Hosting;

namespace Intentify.Shared.Observability.Tests;

public sealed class ObservabilityTests
{
    [Fact]
    public void AddObservability_DoesNotThrow()
    {
        var builder = Host.CreateApplicationBuilder();

        var exception = Record.Exception(() => builder.AddObservability());

        Assert.Null(exception);
    }
}
