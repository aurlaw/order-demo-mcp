using Microsoft.EntityFrameworkCore;
using OrderDemo.Api.Data;
using OrderDemo.Api.Models;
using OrderDemo.Core.DTOs;
using System.Text;
using System.Text.Json;

namespace OrderDemo.Api.Services;

public class OrderService(AppDbContext db, ILogger<OrderService> logger)
{
    private IQueryable<Order> BuildOrderQuery(
        string? lastName, DateTime? from, DateTime? to)
    {
        var query = db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(lastName))
            query = query.Where(o =>
                o.Customer.LastName.ToLower().Contains(lastName.ToLower()));

        if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)   query = query.Where(o => o.OrderDate < to.Value.AddDays(1));

        return query;
    }

    private static OrderDto ProjectOrder(Order o) => new(
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
                l.Product.Price))));

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(
        int page, int pageSize, string? lastName, DateTime? from, DateTime? to)
    {
        logger.LogDebug(
            "Fetching orders page {Page} of size {PageSize} with filters " +
            "lastName={LastName} from={From} to={To}",
            page, pageSize, lastName, from, to);

        var query      = BuildOrderQuery(lastName, from, to);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var raw = await query
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = raw.Select(ProjectOrder).ToList();

        logger.LogInformation(
            "Orders query returned {Count} of {Total} results",
            items.Count, totalCount);

        return new PagedResult<OrderDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<CursorPagedResult<OrderDto>> GetOrdersWithCursorAsync(
        string?   cursor,
        int       pageSize,
        string?   lastName,
        DateTime? from,
        DateTime? to)
    {
        logger.LogDebug(
            "Cursor orders query pageSize={PageSize} hasCursor={HasCursor} " +
            "lastName={LastName} from={From} to={To}",
            pageSize, cursor is not null, lastName, from, to);

        var query = BuildOrderQuery(lastName, from, to);

        if (cursor is not null)
        {
            var decoded = DecodeCursor(cursor);
            query = query.Where(o =>
                o.OrderDate < decoded.LastOrderDate ||
                (o.OrderDate == decoded.LastOrderDate && o.Id < decoded.LastOrderId));
        }

        var raw = await query
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Take(pageSize + 1)
            .ToListAsync();

        var hasMore   = raw.Count > pageSize;
        var pageItems = raw.Take(pageSize).Select(ProjectOrder).ToList();

        string? nextCursor = null;

        if (hasMore && pageItems.Count > 0)
            nextCursor = EncodeCursor(
                new OrderCursor(pageItems.Last().OrderDate, pageItems.Last().Id));

        logger.LogInformation(
            "Cursor query returned {Count} items hasMore={HasMore}",
            pageItems.Count, hasMore);

        return new CursorPagedResult<OrderDto>(pageItems, nextCursor, hasMore);
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        logger.LogDebug("Looking up order {OrderNumber}", orderNumber);

        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order is null)
        {
            logger.LogWarning("Order not found: {OrderNumber}", orderNumber);
            return null;
        }

        logger.LogDebug("Order {OrderNumber} found", orderNumber);
        return ProjectOrder(order);
    }

    public async Task<OrderStatsDto> GetStatsAsync(DateTime? from, DateTime? to)
    {
        logger.LogDebug("Computing order stats from={From} to={To}", from, to);

        var query = db.Orders.AsQueryable();

        if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)   query = query.Where(o => o.OrderDate < to.Value.AddDays(1));

        var totalOrders = await query.CountAsync();
        var totalValue  = totalOrders > 0 ? await query.SumAsync(o => o.Total) : 0m;
        var avgValue    = totalOrders > 0 ? Math.Round(totalValue / totalOrders, 2) : 0m;

        var stats = new OrderStatsDto(totalOrders, Math.Round(totalValue, 2), avgValue, from, to);

        logger.LogInformation(
            "Stats computed: {TotalOrders} orders totalling {TotalValue:C}",
            stats.TotalOrders, stats.TotalValue);

        return stats;
    }

    public async Task<IEnumerable<TopCustomerDto>> GetTopCustomersAsync(
        DateTime? from, DateTime? to, int limit)
    {
        logger.LogDebug(
            "Fetching top {Limit} customers from={From} to={To}",
            limit, from, to);

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

        var result = grouped.Select(g => new TopCustomerDto(
            g.CustomerId,
            customers[g.CustomerId].FirstName,
            customers[g.CustomerId].LastName,
            customers[g.CustomerId].Email,
            g.OrderCount,
            Math.Round(g.TotalSpend, 2))).ToList();

        logger.LogInformation(
            "Top customers query returned {Count} results", result.Count);

        return result;
    }

    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(int customerId)
    {
        logger.LogDebug("Fetching customer summary for CustomerId={CustomerId}", customerId);

        var customer = await db.Customers.FindAsync(customerId);
        if (customer is null)
        {
            logger.LogWarning("Customer not found: {CustomerId}", customerId);
            return null;
        }

        var orders = await db.Orders
            .Where(o => o.CustomerId == customerId)
            .ToListAsync();

        if (orders.Count == 0)
            return new CustomerSummaryDto(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                0, 0m,
                DateTime.MinValue,
                DateTime.MinValue);

        return new CustomerSummaryDto(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            orders.Count,
            Math.Round(orders.Sum(o => o.Total), 2),
            orders.Min(o => o.OrderDate),
            orders.Max(o => o.OrderDate));
    }

    private static string EncodeCursor(OrderCursor cursor) =>
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));

    private static OrderCursor DecodeCursor(string cursor) =>
        JsonSerializer.Deserialize<OrderCursor>(
            Encoding.UTF8.GetString(Convert.FromBase64String(cursor)))!;
}
