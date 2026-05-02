using Microsoft.EntityFrameworkCore;
using OrderDemo.Api.Data;
using OrderDemo.Api.DTOs;

namespace OrderDemo.Api.Services;

public class OrderService(AppDbContext db)
{
    public async Task<PagedResult<OrderDto>> GetOrdersAsync(
        int page, int pageSize, string? lastName, DateTime? from, DateTime? to)
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

        return new PagedResult<OrderDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        return await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .Where(o => o.OrderNumber == orderNumber)
            .Select(o => new OrderDto(
                o.Id,
                o.OrderNumber,
                o.OrderDate,
                o.Total,
                new CustomerDto(
                    o.Customer.Id,
                    o.Customer.FirstName,
                    o.Customer.LastName,
                    o.Customer.Email),
                o.Lines.Select(l => new OrderLineDto(
                    l.Quantity,
                    l.Price,
                    new ProductDto(
                        l.Product.Id,
                        l.Product.Name,
                        l.Product.Price)))))
            .FirstOrDefaultAsync();
    }

    public async Task<OrderStatsDto> GetStatsAsync(DateTime? from, DateTime? to)
    {
        var query = db.Orders.AsQueryable();

        if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)   query = query.Where(o => o.OrderDate < to.Value.AddDays(1));

        var totalOrders = await query.CountAsync();
        var totalValue  = totalOrders > 0 ? await query.SumAsync(o => o.Total) : 0m;
        var avgValue    = totalOrders > 0 ? Math.Round(totalValue / totalOrders, 2) : 0m;

        return new OrderStatsDto(totalOrders, Math.Round(totalValue, 2), avgValue, from, to);
    }

    public async Task<IEnumerable<TopCustomerDto>> GetTopCustomersAsync(
        DateTime? from, DateTime? to, int limit)
    {
        var query = db.Orders.AsQueryable();

        if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)   query = query.Where(o => o.OrderDate < to.Value.AddDays(1));

        var grouped = await query
            .GroupBy(o => o.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                OrderCount = g.Count(),
                TotalSpend = g.Sum(o => o.Total)
            })
            .OrderByDescending(g => g.TotalSpend)
            .Take(limit)
            .ToListAsync();

        var customerIds = grouped.Select(g => g.CustomerId).ToList();
        var customers   = await db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        return grouped.Select(g => new TopCustomerDto(
            g.CustomerId,
            customers[g.CustomerId].FirstName,
            customers[g.CustomerId].LastName,
            customers[g.CustomerId].Email,
            g.OrderCount,
            Math.Round(g.TotalSpend, 2)));
    }
}
