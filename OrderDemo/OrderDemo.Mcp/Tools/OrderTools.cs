using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OrderDemo.Mcp.Services;

namespace OrderDemo.Mcp.Tools;

[McpServerToolType]
public class OrderTools(ApiClient apiClient)
{
    [McpServerTool(Name = "search_orders")]
    [Description("Search and paginate orders. Use to find orders matching optional criteria: " +
                 "customer last name (partial match) and/or a date range. " +
                 "Returns order numbers, dates, customer details, line items, and totals.")]
    public async Task<string> SearchOrdersAsync(
        [Description("Filter by customer last name. Partial match, case-insensitive. " +
                     "Example: 'Smith' returns all orders for customers with 'Smith' in their last name.")]
        string? lastName = null,

        [Description("Start date filter in ISO 8601 format (e.g. 2025-01-01). Inclusive.")]
        string? from = null,

        [Description("End date filter in ISO 8601 format (e.g. 2025-12-31). Inclusive.")]
        string? to = null,

        [Description("Page number for pagination. Starts at 1. Default: 1.")]
        int page = 1,

        [Description("Number of results per page. Default: 20. Max recommended: 50.")]
        int pageSize = 20)
    {
        var result = await apiClient.GetOrdersAsync(lastName, from, to, page, pageSize);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "get_order_detail")]
    [Description("Retrieve complete details for a single order by its order number. " +
                 "Use when you need full information about a specific order: " +
                 "all line items, quantities, unit prices, customer contact details, and order total.")]
    public async Task<string> GetOrderDetailAsync(
        [Description("The order number to look up. Format: ORD-XXXXXX (e.g. ORD-000123).")]
        string orderNumber)
    {
        var result = await apiClient.GetOrderByNumberAsync(orderNumber);
        return result.HasValue
            ? JsonSerializer.Serialize(result.Value)
            : $"No order found with order number '{orderNumber}'.";
    }

    [McpServerTool(Name = "get_order_stats")]
    [Description("Get aggregate order statistics: total number of orders, total revenue, " +
                 "and average order value. Optionally scoped to a date range. " +
                 "Use for high-level summaries, revenue reporting, and trend comparisons.")]
    public async Task<string> GetOrderStatsAsync(
        [Description("Start date in ISO 8601 format (e.g. 2025-01-01). Inclusive. " +
                     "Omit for all-time stats.")]
        string? from = null,

        [Description("End date in ISO 8601 format (e.g. 2025-12-31). Inclusive. " +
                     "Omit for all-time stats.")]
        string? to = null)
    {
        var result = await apiClient.GetOrderStatsAsync(from, to);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "get_top_customers")]
    [Description("Get a ranked list of customers ordered by total spend (highest first). " +
                 "Returns customer name, email, number of orders, and total amount spent. " +
                 "Optionally scoped to a date range. " +
                 "Use to identify highest-value customers or analyse spending patterns.")]
    public async Task<string> GetTopCustomersAsync(
        [Description("Start date in ISO 8601 format. Inclusive. Omit for all-time ranking.")]
        string? from = null,

        [Description("End date in ISO 8601 format. Inclusive. Omit for all-time ranking.")]
        string? to = null,

        [Description("Maximum number of customers to return. Default: 10.")]
        int limit = 10)
    {
        var result = await apiClient.GetTopCustomersAsync(from, to, limit);
        return JsonSerializer.Serialize(result);
    }
}
