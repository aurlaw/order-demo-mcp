using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderDemo.Api.Data;

namespace OrderDemo.Api.HealthChecks;

public class DatabaseHealthCheck(
    AppDbContext                  db,
    ILogger<DatabaseHealthCheck>  logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        HealthCheckResult result;

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                result = HealthCheckResult.Unhealthy("Cannot connect to database.");
            }
            else
            {
                var customerCount = await db.Customers.CountAsync(cancellationToken);
                var orderCount    = await db.Orders.CountAsync(cancellationToken);

                if (customerCount == 0 || orderCount == 0)
                    result = HealthCheckResult.Degraded(
                        $"Database connected but appears unseeded. " +
                        $"Customers: {customerCount}, Orders: {orderCount}");
                else
                    result = HealthCheckResult.Healthy(
                        $"Database healthy. Customers: {customerCount}, Orders: {orderCount}");
            }
        }
        catch (Exception ex)
        {
            result = HealthCheckResult.Unhealthy("Database check threw an exception.", ex);
        }

        logger.LogDebug(
            "Database health check: {Status} — {Description}",
            result.Status, result.Description);

        return result;
    }
}
