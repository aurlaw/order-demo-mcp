using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrderDemo.Core.DTOs;

namespace OrderDemo.Mcp.Services;

public class ApiClient(
    HttpClient         httpClient,
    IConfiguration     config,
    ILogger<ApiClient> logger)
{
    private string?  _token       = null;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private record LoginResponse(string Token, DateTime ExpiresAt);

    public async Task InitializeAsync() => await RefreshTokenAsync();

    private async Task EnsureTokenAsync()
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-30)) return;

        logger.LogDebug("Token expiring soon or missing — refreshing");
        await RefreshTokenAsync();
    }

    private async Task RefreshTokenAsync()
    {
        logger.LogInformation("Refreshing API authentication token");

        var response = await httpClient.PostAsJsonAsync("/auth/login", new
        {
            username = config["ApiClient:Username"],
            password = config["ApiClient:Password"]
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("Login response was empty.");

        _token       = result.Token;
        _tokenExpiry = result.ExpiresAt;

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        logger.LogInformation(
            "API token refreshed successfully, expires at {ExpiresAt}",
            _tokenExpiry);
    }

    private void AttachCorrelationHeader()
    {
        var correlationId = Activity.Current?.TraceId.ToString()
            ?? Guid.NewGuid().ToString();

        httpClient.DefaultRequestHeaders.Remove("X-Correlation-Id");
        httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
    }

    public async Task<CursorPagedResult<OrderDto>> GetOrdersWithCursorAsync(
        string? cursor, int pageSize, string? lastName, string? from, string? to)
    {
        logger.LogDebug(
            "Calling API orders/cursor pageSize={PageSize} hasCursor={HasCursor} " +
            "lastName={LastName} from={From} to={To}",
            pageSize, cursor is not null, lastName, from, to);

        await EnsureTokenAsync();
        AttachCorrelationHeader();

        var qs = BuildQueryString(
            ("pageSize", pageSize.ToString()),
            ("cursor",   cursor),
            ("lastName", lastName),
            ("from",     from),
            ("to",       to));

        var result = await httpClient.GetFromJsonAsync<CursorPagedResult<OrderDto>>(
            $"/orders/cursor{qs}")
            ?? throw new InvalidOperationException("Empty response from cursor endpoint.");

        logger.LogDebug(
            "API orders/cursor returned {Count} items hasMore={HasMore}",
            result.Items.Count(), result.HasMore);

        return result;
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        logger.LogDebug("Calling API orders/{OrderNumber}", orderNumber);

        await EnsureTokenAsync();
        AttachCorrelationHeader();

        var response = await httpClient.GetAsync(
            $"/orders/{Uri.EscapeDataString(orderNumber)}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Order not found via API: {OrderNumber}", orderNumber);
            return null;
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "API call failed: {Method} {Url} returned {StatusCode}",
                response.RequestMessage?.Method,
                response.RequestMessage?.RequestUri,
                (int)response.StatusCode);
            throw;
        }

        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<OrderStatsDto> GetOrderStatsAsync(string? from, string? to)
    {
        logger.LogDebug("Calling API orders/stats from={From} to={To}", from, to);

        await EnsureTokenAsync();
        AttachCorrelationHeader();

        var qs = BuildQueryString(("from", from), ("to", to));
        var stats = await httpClient.GetFromJsonAsync<OrderStatsDto>($"/orders/stats{qs}")
            ?? throw new InvalidOperationException("Empty response from stats endpoint.");

        logger.LogDebug(
            "API orders/stats returned {TotalOrders} orders totalling {TotalValue:C}",
            stats.TotalOrders, stats.TotalValue);

        return stats;
    }

    public async Task<IEnumerable<TopCustomerDto>> GetTopCustomersAsync(
        string? from, string? to, int limit)
    {
        logger.LogDebug(
            "Calling API orders/top-customers limit={Limit} from={From} to={To}",
            limit, from, to);

        await EnsureTokenAsync();
        AttachCorrelationHeader();

        var qs = BuildQueryString(
            ("from",  from),
            ("to",    to),
            ("limit", limit.ToString()));

        var customers = await httpClient.GetFromJsonAsync<IEnumerable<TopCustomerDto>>(
            $"/orders/top-customers{qs}")
            ?? [];

        var list = customers.ToList();
        logger.LogDebug("API top-customers returned {Count} results", list.Count);

        return list;
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
