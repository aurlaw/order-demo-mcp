using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OrderDemo.Core.DTOs;
using OrderDemo.Mcp.Services;

namespace OrderDemo.Mcp.Tools;

[McpServerToolType]
public class OrderTools(ApiClient apiClient)
{
    [McpServerTool(Name = "search_orders")]
    [Description("Search orders using cursor-based pagination. Pass nextCursor from the " +
                 "previous response to retrieve the next page. Stop when nextCursor is null. " +
                 "Optionally filter by customer last name and/or date range.")]
    public async Task<IEnumerable<ContentBlock>> SearchOrdersAsync(
        [Description("Cursor from the previous response. Omit for the first page.")]
        string? cursor = null,

        [Description("Filter by customer last name. Partial match, case-insensitive.")]
        string? lastName = null,

        [Description("Start date in ISO 8601 format (e.g. 2025-01-01). Inclusive.")]
        string? from = null,

        [Description("End date in ISO 8601 format (e.g. 2025-12-31). Inclusive.")]
        string? to = null,

        [Description("Results per page. Default: 20. Max recommended: 50.")]
        int pageSize = 20)
    {
        var result = await apiClient.GetOrdersWithCursorAsync(
            cursor, pageSize, lastName, from, to);

        var summary = $"Returned {result.Items.Count()} orders. " +
                      (result.HasMore
                          ? "More results available — pass nextCursor to continue."
                          : "No more results.");

        return
        [
            new TextContentBlock { Text = summary },
            new TextContentBlock
            {
                Text = JsonSerializer.Serialize(new
                {
                    items      = result.Items,
                    nextCursor = result.NextCursor,
                    hasMore    = result.HasMore
                })
            }
        ];
    }

    [McpServerTool(Name = "get_order_detail")]
    [Description("Retrieve complete details for a single order by its order number. " +
                 "Returns all line items, quantities, unit prices, customer details, and total.")]
    public async Task<IEnumerable<ContentBlock>> GetOrderDetailAsync(
        [Description("The order number to look up. Format: ORD-XXXXXX (e.g. ORD-000123).")]
        string orderNumber)
    {
        var order = await apiClient.GetOrderByNumberAsync(orderNumber);

        if (order is null)
            return [new TextContentBlock { Text = $"No order found with number '{orderNumber}'." }];

        var linesSummary = string.Join("\n", order.Lines.Select(l =>
            $"  - {l.Product.Name} x{l.Quantity} @ {l.Price:C} = {l.Price * l.Quantity:C}"));

        var summary = $"""
            Order {order.OrderNumber}
            Date:     {order.OrderDate:yyyy-MM-dd}
            Customer: {order.Customer.FirstName} {order.Customer.LastName} ({order.Customer.Email})
            Total:    {order.Total:C}
            Lines:
            {linesSummary}
            """;

        return
        [
            new TextContentBlock { Text = summary },
            new TextContentBlock { Text = JsonSerializer.Serialize(order) }
        ];
    }

    [McpServerTool(Name = "get_order_stats")]
    [Description("Get aggregate order statistics: total orders, total revenue, and average " +
                 "order value. Optionally scoped to a date range.")]
    public async Task<IEnumerable<ContentBlock>> GetOrderStatsAsync(
        [Description("Start date in ISO 8601 format. Inclusive. Omit for all-time stats.")]
        string? from = null,

        [Description("End date in ISO 8601 format. Inclusive. Omit for all-time stats.")]
        string? to = null)
    {
        var stats  = await apiClient.GetOrderStatsAsync(from, to);
        var period = from is not null ? $" from {from} to {to}" : " (all time)";

        var summary = $"""
            Order statistics{period}:
            - Total orders:        {stats.TotalOrders:N0}
            - Total revenue:       {stats.TotalValue:C}
            - Average order value: {stats.AverageOrderValue:C}
            """;

        return
        [
            new TextContentBlock { Text = summary },
            new TextContentBlock { Text = JsonSerializer.Serialize(stats) }
        ];
    }

    [McpServerTool(Name = "get_top_customers")]
    [Description("Get customers ranked by total spend, highest first. Optionally scoped " +
                 "to a date range.")]
    public async Task<IEnumerable<ContentBlock>> GetTopCustomersAsync(
        [Description("Start date in ISO 8601 format. Inclusive. Omit for all-time ranking.")]
        string? from = null,

        [Description("End date in ISO 8601 format. Inclusive. Omit for all-time ranking.")]
        string? to = null,

        [Description("Maximum number of customers to return. Default: 10.")]
        int limit = 10)
    {
        var customers = (await apiClient.GetTopCustomersAsync(from, to, limit)).ToList();
        var period    = from is not null ? $" from {from} to {to}" : " (all time)";

        var rows = string.Join("\n", customers.Select((c, i) =>
            $"  {i + 1}. {c.FirstName} {c.LastName} — {c.TotalSpend:C} across {c.OrderCount} orders"));

        return
        [
            new TextContentBlock { Text = $"Top {customers.Count} customers by spend{period}:\n{rows}" },
            new TextContentBlock { Text = JsonSerializer.Serialize(customers) }
        ];
    }

    [McpServerTool(Name = "generate_insights")]
    [Description("Fetch order statistics and top customers for a period, then generate an " +
                 "AI-powered narrative analysis highlighting trends, anomalies, or opportunities. " +
                 "Use when qualitative interpretation is needed rather than raw numbers.")]
    public async Task<string> GenerateInsightsAsync(
        McpServer         server,
        CancellationToken cancellationToken,

        [Description("Start date in ISO 8601 format. Omit for all-time analysis.")]
        string? from = null,

        [Description("End date in ISO 8601 format. Omit for all-time analysis.")]
        string? to = null)
    {
        if (server.ClientCapabilities?.Sampling is null)
            return "This MCP client does not advertise sampling support, so generate_insights " +
                   "cannot call the LLM for a narrative analysis. Use get_order_stats and " +
                   "get_top_customers to retrieve the raw numbers instead.";

        var stats     = await apiClient.GetOrderStatsAsync(from, to);
        var customers = (await apiClient.GetTopCustomersAsync(from, to, 5)).ToList();
        var period    = from is not null ? $"{from} to {to}" : "all time";

        var prompt = $"""
            You are a business analyst reviewing order data for the period: {period}.

            Order statistics:
            - Total orders: {stats.TotalOrders:N0}
            - Total revenue: {stats.TotalValue:C}
            - Average order value: {stats.AverageOrderValue:C}

            Top 5 customers by spend:
            {string.Join("\n", customers.Select((c, i) =>
                $"{i + 1}. {c.FirstName} {c.LastName}: {c.TotalSpend:C} across {c.OrderCount} orders"))}

            Provide a 3-4 sentence narrative insight. Highlight anything notable —
            revenue concentration, order frequency patterns, or opportunities.
            Be specific and use the actual numbers.
            """;

        ChatMessage[] messages = [new(ChatRole.User, prompt)];
        ChatOptions   options  = new() { MaxOutputTokens = 300 };

        var response = await server.AsSamplingChatClient()
            .GetResponseAsync(messages, options, cancellationToken);

        return response.Text ?? "No insight generated.";
    }
}
