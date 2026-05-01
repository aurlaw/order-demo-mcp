using Microsoft.EntityFrameworkCore;
using OrderDemo.Api.Data;
using OrderDemo.Api.DTOs;

namespace OrderDemo.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/orders", async (
            AppDbContext db,
            int page = 1,
            int pageSize = 20,
            string? lastName = null,
            DateTime? from = null,
            DateTime? to = null) =>
        {
            var query = db.Orders
                .Include(o => o.Customer)
                .Include(o => o.Lines).ThenInclude(l => l.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(lastName))
                query = query.Where(o => o.Customer.LastName.ToLower().Contains(lastName.ToLower()));

            if (from.HasValue)
                query = query.Where(o => o.OrderDate >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.OrderDate < to.Value.AddDays(1));

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto(
                    o.Id,
                    o.OrderNumber,
                    o.OrderDate,
                    o.Total,
                    new CustomerDto(o.Customer.Id, o.Customer.FirstName, o.Customer.LastName, o.Customer.Email),
                    o.Lines.Select(l => new OrderLineDto(
                        l.Quantity,
                        l.Price,
                        new ProductDto(l.Product.Id, l.Product.Name, l.Product.Price)
                    ))
                ))
                .ToListAsync();

            return Results.Ok(new PagedResult<OrderDto>(items, totalCount, page, pageSize, totalPages));
        })
        .WithName("GetOrders")
        .WithSummary("Get paginated orders")
        .WithDescription("Returns paginated orders. Filter by customer last name and/or date range.")
        .RequireAuthorization();
    }
}
