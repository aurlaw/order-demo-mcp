using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderDemo.Mcp.Services;

public class Auth0ManagementService(
    IHttpClientFactory              httpClientFactory,
    IConfiguration                  config,
    ILogger<Auth0ManagementService> logger)
{
    private string?  _managementToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private async Task EnsureManagementTokenAsync()
    {
        if (_managementToken is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            return;

        logger.LogInformation("Refreshing Auth0 Management API token");

        var domain   = config["Auth0:Domain"]!;
        var clientId = config["Auth0:Management:ClientId"]!;
        var secret   = config["Auth0:Management:ClientSecret"]!;

        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsJsonAsync(
            $"https://{domain}/oauth/token",
            new
            {
                client_id     = clientId,
                client_secret = secret,
                audience      = $"https://{domain}/api/v2/",
                grant_type    = "client_credentials"
            });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        _managementToken = result.GetProperty("access_token").GetString()!;
        _tokenExpiry     = DateTime.UtcNow.AddSeconds(
            result.GetProperty("expires_in").GetInt32());

        logger.LogInformation(
            "Management token refreshed, expires at {ExpiresAt}", _tokenExpiry);
    }

    public async Task<JsonElement> CreateClientAsync(
        string clientName, string[] redirectUris)
    {
        await EnsureManagementTokenAsync();

        logger.LogInformation("Creating Auth0 client for {ClientName}", clientName);

        var domain = config["Auth0:Domain"]!;
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _managementToken);

        var response = await httpClient.PostAsJsonAsync(
            $"https://{domain}/api/v2/clients",
            new
            {
                name          = clientName,
                app_type      = "regular_web",
                callbacks     = redirectUris,
                grant_types   = new[] { "authorization_code", "refresh_token" },
                token_endpoint_auth_method = "client_secret_post",
                oidc_conformant = true
            });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task DeleteClientAsync(string clientId)
    {
        await EnsureManagementTokenAsync();

        logger.LogInformation("Deleting Auth0 client {ClientId}", clientId);

        var domain = config["Auth0:Domain"]!;
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _managementToken);

        await httpClient.DeleteAsync($"https://{domain}/api/v2/clients/{clientId}");
    }
}
