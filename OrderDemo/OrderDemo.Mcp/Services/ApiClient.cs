using System.Net.Http.Headers;
using System.Net.Http.Json;
using OrderDemo.Core.DTOs;

namespace OrderDemo.Mcp.Services;

public class ApiClient(HttpClient httpClient, IConfiguration config)
{
    private string?  _token       = null;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private record LoginResponse(string Token, DateTime ExpiresAt);

    public async Task InitializeAsync() => await RefreshTokenAsync();

    private async Task EnsureTokenAsync()
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-30)) return;
        await RefreshTokenAsync();
    }

    private async Task RefreshTokenAsync()
    {
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
    }

    public async Task<CursorPagedResult<OrderDto>> GetOrdersWithCursorAsync(
        string? cursor, int pageSize, string? lastName, string? from, string? to)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(
            ("pageSize", pageSize.ToString()),
            ("cursor",   cursor),
            ("lastName", lastName),
            ("from",     from),
            ("to",       to));
        return await httpClient.GetFromJsonAsync<CursorPagedResult<OrderDto>>(
            $"/orders/cursor{qs}")
            ?? throw new InvalidOperationException("Empty response from cursor endpoint.");
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        await EnsureTokenAsync();
        var response = await httpClient.GetAsync(
            $"/orders/{Uri.EscapeDataString(orderNumber)}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    public async Task<OrderStatsDto> GetOrderStatsAsync(string? from, string? to)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(("from", from), ("to", to));
        return await httpClient.GetFromJsonAsync<OrderStatsDto>($"/orders/stats{qs}")
            ?? throw new InvalidOperationException("Empty response from stats endpoint.");
    }

    public async Task<IEnumerable<TopCustomerDto>> GetTopCustomersAsync(
        string? from, string? to, int limit)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(
            ("from",  from),
            ("to",    to),
            ("limit", limit.ToString()));
        return await httpClient.GetFromJsonAsync<IEnumerable<TopCustomerDto>>(
            $"/orders/top-customers{qs}")
            ?? [];
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
