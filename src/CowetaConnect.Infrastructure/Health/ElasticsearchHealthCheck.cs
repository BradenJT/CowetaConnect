using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CowetaConnect.Infrastructure.Health;

public sealed class ElasticsearchHealthCheck(ElasticsearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.PingAsync(cancellationToken: cancellationToken);
            return response.IsValidResponse
                ? HealthCheckResult.Healthy("Elasticsearch is reachable.")
                : HealthCheckResult.Unhealthy("Elasticsearch ping failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch is unreachable.", ex);
        }
    }
}
