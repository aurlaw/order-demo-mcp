using OrderDemo.Api.Services;

namespace OrderDemo.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization("ApiAccess");

        group.MapGet("/orders/stats", async (
            OrderService orderService,
            DateTime? from = null,
            DateTime? to   = null) =>
        {
            var stats = await orderService.GetStatsAsync(from, to);
            return Results.Ok(stats);
        })
        .WithName("GetOrderStats")
        .WithSummary("Get aggregate order statistics")
        .WithDescription("Returns total order count, total value, and average order value. Optionally scoped to a date range.");

        group.MapGet("/orders/top-customers", async (
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
        .WithDescription("Returns customers ranked by total spend within an optional date range.");

        group.MapGet("/orders/cursor", async (
            OrderService orderService,
            string?   cursor   = null,
            int       pageSize = 20,
            string?   lastName = null,
            DateTime? from     = null,
            DateTime? to       = null) =>
        {
            var result = await orderService.GetOrdersWithCursorAsync(
                cursor, pageSize, lastName, from, to);
            return Results.Ok(result);
        })
        .WithName("GetOrdersCursor")
        .WithSummary("Get orders using cursor-based pagination")
        .WithDescription("Keyset pagination. Pass nextCursor from the previous response for the next page. " +
                         "Null nextCursor indicates no more results.");

        group.MapGet("/orders/{orderNumber}", async (
            string       orderNumber,
            OrderService orderService) =>
        {
            var order = await orderService.GetOrderByNumberAsync(orderNumber);
            return order is null ? Results.NotFound() : Results.Ok(order);
        })
        .WithName("GetOrderByNumber")
        .WithSummary("Get a single order by order number")
        .WithDescription("Returns full order detail including customer and all line items.");

        group.MapGet("/orders", async (
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
        .WithDescription("Returns paginated orders. Filter by customer last name and/or date range.");

        group.MapGet("/customers/{customerId:int}/summary", async (
            int          customerId,
            OrderService orderService) =>
        {
            var summary = await orderService.GetCustomerSummaryAsync(customerId);
            return summary is null ? Results.NotFound() : Results.Ok(summary);
        })
        .WithName("GetCustomerSummary")
        .WithSummary("Get order summary for a customer")
        .WithDescription("Returns aggregate order data for a specific customer.");
    }
}
