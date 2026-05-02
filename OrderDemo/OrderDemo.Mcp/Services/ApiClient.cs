using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderDemo.Mcp.Services;

public class ApiClient(HttpClient httpClient, IConfiguration config)
{
    private string?  _token       = null;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private record LoginResponse(string Token, DateTime ExpiresAt);

    public async Task InitializeAsync()
    {
        await RefreshTokenAsync();
    }

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

    public async Task<JsonElement> GetOrdersAsync(
        string? lastName, string? from, string? to, int page, int pageSize)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(
            ("page",     page.ToString()),
            ("pageSize", pageSize.ToString()),
            ("lastName", lastName),
            ("from",     from),
            ("to",       to));

        return await httpClient.GetFromJsonAsync<JsonElement>($"/orders{qs}");
    }

    public async Task<JsonElement?> GetOrderByNumberAsync(string orderNumber)
    {
        await EnsureTokenAsync();
        var response = await httpClient.GetAsync($"/orders/{Uri.EscapeDataString(orderNumber)}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<JsonElement> GetOrderStatsAsync(string? from, string? to)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(("from", from), ("to", to));
        return await httpClient.GetFromJsonAsync<JsonElement>($"/orders/stats{qs}");
    }

    public async Task<JsonElement> GetTopCustomersAsync(string? from, string? to, int limit)
    {
        await EnsureTokenAsync();
        var qs = BuildQueryString(
            ("from",  from),
            ("to",    to),
            ("limit", limit.ToString()));
        return await httpClient.GetFromJsonAsync<JsonElement>($"/orders/top-customers{qs}");
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
