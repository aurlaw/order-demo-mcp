using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using OrderDemo.Core.DTOs;

namespace OrderDemo.Mcp.Services;

public class ApiClient(
    HttpClient             httpClient,
    IHttpContextAccessor   httpContextAccessor,
    ILogger<ApiClient>     logger)
{
    private async Task SetAuthHeaderAsync()
    {
        var token = await httpContextAccessor.HttpContext!
            .GetTokenAsync("access_token");

        if (string.IsNullOrEmpty(token))
            throw new UnauthorizedAccessException(
                "No access token present in request context.");

        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        logger.LogDebug("Auth header set from request context token");
    }

    public async Task<CursorPagedResult<OrderDto>> GetOrdersWithCursorAsync(
        string? cursor, int pageSize, string? lastName, string? from, string? to)
    {
        await SetAuthHeaderAsync();
        AttachCorrelationHeader();

        logger.LogDebug(
            "Calling API orders/cursor pageSize={PageSize} hasCursor={HasCursor}",
            pageSize, cursor is not null);

        var qs = BuildQueryString(
            ("pageSize", pageSize.ToString()),
            ("cursor",   cursor),
            ("lastName", lastName),
            ("from",     from),
            ("to",       to));

        return await httpClient.GetFromJsonAsync<CursorPagedResult<OrderDto>>(
            $"/api/orders/cursor{qs}")
            ?? throw new InvalidOperationException("Empty response from cursor endpoint.");
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        await SetAuthHeaderAsync();
        AttachCorrelationHeader();

        logger.LogDebug("Calling API order detail for {OrderNumber}", orderNumber);

        var response = await httpClient.GetAsync(
            $"/api/orders/{Uri.EscapeDataString(orderNumber)}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<OrderStatsDto> GetOrderStatsAsync(string? from, string? to)
    {
        await SetAuthHeaderAsync();
        AttachCorrelationHeader();

        logger.LogDebug("Calling API order stats from={From} to={To}", from, to);

        var qs = BuildQueryString(("from", from), ("to", to));
        return await httpClient.GetFromJsonAsync<OrderStatsDto>($"/api/orders/stats{qs}")
            ?? throw new InvalidOperationException("Empty response from stats endpoint.");
    }

    public async Task<IEnumerable<TopCustomerDto>> GetTopCustomersAsync(
        string? from, string? to, int limit)
    {
        await SetAuthHeaderAsync();
        AttachCorrelationHeader();

        logger.LogDebug(
            "Calling API top customers limit={Limit} from={From} to={To}",
            limit, from, to);

        var qs = BuildQueryString(
            ("from",  from),
            ("to",    to),
            ("limit", limit.ToString()));
        return await httpClient.GetFromJsonAsync<IEnumerable<TopCustomerDto>>(
            $"/api/orders/top-customers{qs}")
            ?? [];
    }

    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(int customerId)
    {
        await SetAuthHeaderAsync();
        AttachCorrelationHeader();

        logger.LogDebug("Calling API customer summary for {CustomerId}", customerId);

        var response = await httpClient.GetAsync($"/api/customers/{customerId}/summary");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerSummaryDto>();
    }

    private void AttachCorrelationHeader()
    {
        var correlationId = System.Diagnostics.Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString();
        httpClient.DefaultRequestHeaders.Remove("X-Correlation-Id");
        httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
    }

    private static string BuildQueryString(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}");
        var qs = string.Join("&", parts);
        return qs.Length > 0 ? $"?{qs}" : string.Empty;
    }
}
