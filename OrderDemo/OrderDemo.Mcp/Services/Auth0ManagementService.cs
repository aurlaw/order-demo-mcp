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

    /// <summary>
    /// Patches the Allowed Callback URLs of the pre-configured "Order Demo MCP" Auth0
    /// application to include any redirect URIs sent by the MCP client (e.g. Claude Desktop).
    /// Merges with the server's own configured callback URL so neither is lost.
    /// Requires the Management M2M app to have the update:clients scope.
    /// </summary>
    public async Task UpdateClientCallbacksAsync(string clientId, string[] redirectUris)
    {
        await EnsureManagementTokenAsync();

        logger.LogInformation("Updating callbacks for Auth0 client {ClientId}", clientId);

        var domain      = config["Auth0:Domain"]!;
        var existingUrl = config["Auth0:Mcp:CallbackUrl"]!;

        // Keep the server's configured callback + whatever the MCP client sends
        var merged = redirectUris
            .Append(existingUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _managementToken);

        var response = await httpClient.PatchAsJsonAsync(
            $"https://{domain}/api/v2/clients/{clientId}",
            new { callbacks = merged });

        response.EnsureSuccessStatusCode();

        logger.LogInformation("Auth0 client {ClientId} callbacks updated: {Callbacks}",
            clientId, string.Join(", ", merged));
    }
}
