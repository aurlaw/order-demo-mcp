using OrderDemo.Api.Services;

namespace OrderDemo.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
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
