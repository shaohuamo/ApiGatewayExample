using CommonService.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ProductsMicroService.API.Health;

sealed class StartupReadinessHealthCheck(IStartupReadinessState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = state.IsReady
            ? HealthCheckResult.Healthy(state.Reason)
            : HealthCheckResult.Unhealthy(state.Reason ?? "Startup dependencies are not ready yet.");

        return Task.FromResult(result);
    }
}
