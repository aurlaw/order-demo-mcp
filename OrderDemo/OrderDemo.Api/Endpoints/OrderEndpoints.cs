using OrderDemo.Api.Services;

namespace OrderDemo.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/orders/stats", async (
            OrderService orderService,
            DateTime? from = null,
            DateTime? to   = null) =>
        {
            var stats = await orderService.GetStatsAsync(from, to);
            return Results.Ok(stats);
        })
        .WithName("GetOrderStats")
        .WithSummary("Get aggregate order statistics")
        .WithDescription("Returns total order count, total value, and average order value. Optionally scoped to a date range.")
        .RequireAuthorization();

        app.MapGet("/orders/top-customers", async (
            OrderService orderService,
            DateTime? from  = null,
            DateTime? to    = null,
            int       limit = 10) =>
        {
            var customers = await orderService.GetTopCustomersAsync(from, to, limit);
            return Results.Ok(customers);
        })
        .WithName("GetTopCustomers")
        .WithSummary("Get top customers by total spend")
        .WithDescription("Returns customers ranked by total spend within an optional date range.")
        .RequireAuthorization();

        app.MapGet("/orders/{orderNumber}", async (
            string       orderNumber,
            OrderService orderService) =>
        {
            var order = await orderService.GetOrderByNumberAsync(orderNumber);
            return order is null ? Results.NotFound() : Results.Ok(order);
        })
        .WithName("GetOrderByNumber")
        .WithSummary("Get a single order by order number")
        .WithDescription("Returns full order detail including customer and all line items.")
        .RequireAuthorization();

        app.MapGet("/orders", async (
            OrderService orders,
            int page = 1,
            int pageSize = 20,
            string? lastName = null,
            DateTime? from = null,
            DateTime? to = null) =>
        {
            var result = await orders.GetOrdersAsync(page, pageSize, lastName, from, to);
            return Results.Ok(result);
        })
        .WithName("GetOrders")
        .WithSummary("Get paginated orders")
        .WithDescription("Returns paginated orders. Filter by customer last name and/or date range.")
        .RequireAuthorization();
    }
}
