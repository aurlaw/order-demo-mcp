using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderDemo.Mcp.HealthChecks;

public class ApiHealthCheck(
    IHttpClientFactory      httpClientFactory,
    ILogger<ApiHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        HealthCheckResult result;

        try
        {
            var client   = httpClientFactory.CreateClient("health");
            var response = await client.GetAsync("http://orderdemo-api/health", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                result = HealthCheckResult.Degraded($"API returned {(int)response.StatusCode}");
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                result = HealthCheckResult.Healthy($"API healthy. Response: {body}");
            }
        }
        catch (HttpRequestException ex)
        {
            result = HealthCheckResult.Degraded("API unreachable.", ex);
        }
        catch (TaskCanceledException ex)
        {
            result = HealthCheckResult.Degraded("API health check timed out.", ex);
        }

        logger.LogDebug(
            "API health check: {Status} — {Description}",
            result.Status, result.Description);

        return result;
    }
}
