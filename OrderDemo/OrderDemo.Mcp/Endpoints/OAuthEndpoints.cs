using System.Text.Json;
using OrderDemo.Mcp.Services;

namespace OrderDemo.Mcp.Endpoints;

public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        // Tells Anthropic's broker where the Auth0 authorization server is
        app.MapGet("/.well-known/oauth-protected-resource", (IConfiguration config) =>
        {
            var domain      = config["Auth0:Domain"]!;
            var callbackUrl = config["Auth0:Mcp:CallbackUrl"]!;
            var serverUrl   = callbackUrl.Replace("/callback", string.Empty);

            return Results.Ok(new
            {
                resource              = serverUrl,
                authorization_servers = new[] { $"https://{domain}" },
                scopes_supported      = new[] { "orders:read" },
                bearer_methods_supported = new[] { "header" }
            });
        });

        // Broker registers itself — we create an OAuth client in Auth0 on its behalf
        app.MapPost("/register", async (
            JsonElement            body,
            Auth0ManagementService managementService,
            IConfiguration         config) =>
        {
            var clientName = body.TryGetProperty("client_name", out var name)
                ? name.GetString() ?? "MCP Client"
                : "MCP Client";

            var redirectUris = body.TryGetProperty("redirect_uris", out var uris)
                ? uris.EnumerateArray()
                       .Select(u => u.GetString()!)
                       .ToArray()
                : Array.Empty<string>();

            var client = await managementService.CreateClientAsync(clientName, redirectUris);

            var domain = config["Auth0:Domain"]!;

            return Results.Ok(new
            {
                client_id     = client.GetProperty("client_id").GetString(),
                client_secret = client.GetProperty("client_secret").GetString(),
                client_name   = clientName,
                redirect_uris = redirectUris,
                token_endpoint_auth_method = "client_secret_post",
                grant_types   = new[] { "authorization_code", "refresh_token" },
                authorization_endpoint = $"https://{domain}/authorize",
                token_endpoint         = $"https://{domain}/oauth/token"
            });
        });
    }
}
