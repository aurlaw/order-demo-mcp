using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
using OrderDemo.Mcp.Endpoints;
using OrderDemo.Mcp.HealthChecks;
using OrderDemo.Mcp.Prompts;
using OrderDemo.Mcp.Resources;
using OrderDemo.Mcp.Services;
using OrderDemo.Mcp.Tools;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder  = WebApplication.CreateBuilder(args);
    var config   = builder.Configuration;
    var useStdio = args.Contains("--stdio");

    builder.Configuration["UseStdio"] = useStdio.ToString();

    builder.AddServiceDefaults();

    builder.Host.UseSerilog((context, services, logConfig) =>
    {
        var cfg = logConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
            .WriteTo.OpenTelemetry(otel =>
            {
                otel.Endpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                    ?? "http://localhost:4317";
                otel.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                otel.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = context.HostingEnvironment.ApplicationName
                };
            });

        if (context.Configuration.GetValue<bool>("UseStdio"))
            cfg.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
    });

    // Token validation — MCP server is a resource server for Auth0 tokens
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = $"https://{config["Auth0:Domain"]}/";
            options.Audience  = config["Auth0:Audience"];
            options.SaveToken = true; // required for GetTokenAsync("access_token") in ApiClient
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    var hasBearer = context.Request.Headers.Authorization
                        .ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                    logger.LogInformation("[JWT] {Method} {Path} — Authorization header present: {HasBearer}",
                        context.Request.Method, context.Request.Path, hasBearer);
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("[JWT] Authentication FAILED: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("[JWT] Token validated. sub={Sub} aud={Aud}",
                        context.Principal?.FindFirst("sub")?.Value,
                        context.Principal?.FindFirst("aud")?.Value);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("[JWT] Challenge issued. AuthFailure: {Failure}",
                        context.AuthenticateFailure?.Message ?? "none (anonymous request)");

                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    var host = $"{context.Request.Scheme}://{context.Request.Host}";
                    context.Response.Headers.Append("WWW-Authenticate",
                        $"Bearer realm=\"{host}\", " +
                        $"resource_metadata=\"{host}/.well-known/oauth-protected-resource\"");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddHttpContextAccessor();

    // ApiClient — typed HTTP client; reads per-request Auth0 token via IHttpContextAccessor
    builder.Services.AddHttpClient<ApiClient>(client =>
        client.BaseAddress = new Uri("http://orderdemo-api"))
        .AddServiceDiscovery()
        .AddStandardResilienceHandler();

    // Health check HTTP client — unauthenticated, used only by ApiHealthCheck
    builder.Services.AddHttpClient("health", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddServiceDiscovery()
    .AddStandardResilienceHandler();

    // Auth0ManagementService — singleton, manages its own management token lifecycle
    builder.Services.AddSingleton<Auth0ManagementService>();

    builder.Services.AddHealthChecks()
        .AddCheck<ApiHealthCheck>(
            name: "api",
            failureStatus: HealthStatus.Degraded,
            tags: ["api", "upstream"]);

    var mcpBuilder = builder.Services
        .AddMcpServer()
        .WithTools<OrderTools>()
        .WithPrompts<OrderPrompts>()
        .WithResources<OrderResources>();

    if (useStdio)
        mcpBuilder.WithStdioServerTransport();
    else
        mcpBuilder.WithHttpTransport();

    var app = builder.Build();

    app.MapDefaultEndpoints();

    app.UseAuthentication();
    app.UseAuthorization();

    // OAuth discovery and DCR — public, no auth required, must come before MapMcp
    app.MapOAuthEndpoints();

    if (!useStdio)
        app.MapMcp().RequireAuthorization();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
