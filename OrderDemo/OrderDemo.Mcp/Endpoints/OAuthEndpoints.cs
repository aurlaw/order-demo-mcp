using System.Text.Json;
using OrderDemo.Mcp.Services;

namespace OrderDemo.Mcp.Endpoints;

public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        // RFC 9728 — tells clients which authorization server protects this resource.
        // resource = Auth0 API identifier so clients request the correct audience.
        app.MapGet("/.well-known/oauth-protected-resource", (
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger     = loggerFactory.CreateLogger("OAuthEndpoints");
            var domain     = config["Auth0:Domain"]!;
            var audience   = config["Auth0:Audience"]!;
            var authServer = $"https://{domain}";

            logger.LogInformation(
                "[OAuth] Resource metadata requested. resource={Resource} auth_server={AuthServer}",
                audience, authServer);

            return Results.Ok(new
            {
                resource                 = audience,
                authorization_servers    = new[] { authServer },
                scopes_supported         = new[] { "orders:read" },
                bearer_methods_supported = new[] { "header" }
            });
        });

        // RFC 8414 — MCP clients look here first to discover all OAuth endpoints.
        // authorization_endpoint points to OUR /authorize proxy so we can inject
        // the Auth0-required audience parameter that Claude doesn't know about.
        app.MapGet("/.well-known/oauth-authorization-server", (
            HttpRequest    request,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger     = loggerFactory.CreateLogger("OAuthEndpoints");
            var domain     = config["Auth0:Domain"]!;
            var authBase   = $"https://{domain}";
            var serverBase = $"{request.Scheme}://{request.Host}";

            logger.LogInformation("[OAuth] AS metadata requested. auth_base={AuthBase}", authBase);

            return Results.Ok(new
            {
                issuer                                  = $"{authBase}/",
                authorization_endpoint                  = $"{serverBase}/authorize",  // our proxy — injects audience
                token_endpoint                          = $"{authBase}/oauth/token",
                userinfo_endpoint                       = $"{authBase}/userinfo",
                jwks_uri                                = $"{authBase}/.well-known/jwks.json",
                registration_endpoint                   = $"{serverBase}/register",
                scopes_supported                        = new[] { "openid", "profile", "email", "orders:read" },
                response_types_supported                = new[] { "code" },
                grant_types_supported                   = new[] { "authorization_code" },
                token_endpoint_auth_methods_supported   = new[] { "client_secret_post", "none" },
                code_challenge_methods_supported        = new[] { "S256" }
            });
        });

        // Authorize proxy — Claude sends the user here; we inject audience=<api-identifier>
        // then redirect to Auth0. Without audience, Auth0 skips the API access token and
        // issues only an encrypted ID token, which our JWT Bearer cannot validate.
        app.MapGet("/authorize", (HttpRequest request, IConfiguration config, ILoggerFactory loggerFactory) =>
        {
            var logger   = loggerFactory.CreateLogger("OAuthEndpoints");
            var domain   = config["Auth0:Domain"]!;
            var audience = config["Auth0:Audience"]!;

            var parts = new List<string>();
            foreach (var (key, values) in request.Query)
            {
                if (key.Equals("audience", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var val in values)
                    parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(val ?? "")}");
            }
            parts.Add($"audience={Uri.EscapeDataString(audience)}");

            var target = $"https://{domain}/authorize?{string.Join('&', parts)}";
            logger.LogInformation("[OAuth] /authorize proxy → {Domain} audience={Audience}", domain, audience);
            return Results.Redirect(target);
        });

        // DCR endpoint — instead of creating a new Auth0 application every connection,
        // we return the pre-configured "Order Demo MCP" credentials and patch its
        // Allowed Callback URLs to include whatever redirect_uri the client sent.
        app.MapPost("/register", async (
            HttpRequest            request,
            JsonElement            body,
            Auth0ManagementService managementService,
            IConfiguration         config,
            ILoggerFactory         loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("OAuthEndpoints");

            var redirectUris = body.TryGetProperty("redirect_uris", out var uris)
                ? uris.EnumerateArray().Select(u => u.GetString()!).ToArray()
                : Array.Empty<string>();

            var clientId     = config["Auth0:Mcp:ClientId"]!;
            var clientSecret = config["Auth0:Mcp:ClientSecret"]!;
            var domain       = config["Auth0:Domain"]!;
            var serverBase   = $"{request.Scheme}://{request.Host}";

            logger.LogInformation("[DCR] Register — returning pre-configured client {ClientId}. " +
                "Redirect URI(s): {Uris}", clientId, string.Join(", ", redirectUris));

            try
            {
                await managementService.UpdateClientCallbacksAsync(clientId, redirectUris);

                return Results.Ok(new
                {
                    client_id                  = clientId,
                    client_secret              = clientSecret,
                    client_name                = "Order Demo MCP",
                    redirect_uris              = redirectUris,
                    token_endpoint_auth_method = "client_secret_post",
                    grant_types                = new[] { "authorization_code" },
                    authorization_endpoint     = $"{serverBase}/authorize",
                    token_endpoint             = $"https://{domain}/oauth/token"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DCR] Failed to update callbacks for {ClientId}", clientId);
                return Results.Problem("Failed to register client", statusCode: 500);
            }
        });
    }
}
