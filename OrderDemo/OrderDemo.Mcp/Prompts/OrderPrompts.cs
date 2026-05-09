using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace OrderDemo.Mcp.Prompts;

[McpServerPromptType]
public class OrderPrompts
{
    [McpServerPrompt(Name = "monthly_order_summary")]
    [Description("Generate a summary report for a specific calendar month")]
    public static IEnumerable<ChatMessage> MonthlyOrderSummary(
        [Description("Month in YYYY-MM format, e.g. 2025-03")]
        string month)
    {
        var date = DateTime.Parse($"{month}-01");
        var from = date.ToString("yyyy-MM-dd");
        var to   = date.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");

        return
        [
            new ChatMessage(ChatRole.User,
                $"Retrieve order stats and the top 10 customers for {from} to {to}. " +
                $"Present as a concise executive summary with key metrics highlighted. " +
                $"Include total orders, total revenue, average order value, " +
                $"and name the top 3 customers with their spend.")
        ];
    }

    [McpServerPrompt(Name = "top_customers_report")]
    [Description("Ranked customer report by total spend for a given date range")]
    public static IEnumerable<ChatMessage> TopCustomersReport(
        [Description("Start date in YYYY-MM-DD format")]
        string from,

        [Description("End date in YYYY-MM-DD format")]
        string to,

        [Description("Number of customers to include. Default: 10")]
        int limit = 10)
    {
        return
        [
            new ChatMessage(ChatRole.User,
                $"Retrieve the top {limit} customers by spend from {from} to {to}. " +
                $"Present as a ranked table showing position, name, email, " +
                $"number of orders, and total spend. Add a brief observation about any notable patterns.")
        ];
    }

    [McpServerPrompt(Name = "order_lookup")]
    [Description("Full detail view for a specific order number")]
    public static IEnumerable<ChatMessage> OrderLookup(
        [Description("Order number in ORD-XXXXXX format, e.g. ORD-000123")]
        string orderNumber)
    {
        return
        [
            new ChatMessage(ChatRole.User,
                $"Look up order {orderNumber} and present the full details: " +
                $"order date, customer name and email, each line item with product name, quantity, " +
                $"unit price and line total, and the order total.")
        ];
    }

    [McpServerPrompt(Name = "daily_briefing")]
    [Description("Order activity summary for the last 7 days with AI-generated insights")]
    public static IEnumerable<ChatMessage> DailyBriefing()
    {
        var to   = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");

        return
        [
            new ChatMessage(ChatRole.User,
                $"Generate a daily briefing for {from} to {to}. " +
                $"Retrieve order stats, top 5 customers, and generate insights for the same period. " +
                $"Format as a briefing document with a metrics section and an insights section.")
        ];
    }
}
