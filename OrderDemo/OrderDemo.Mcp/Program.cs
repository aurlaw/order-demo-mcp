using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
using OrderDemo.Mcp.HealthChecks;
using OrderDemo.Mcp.Prompts;
using OrderDemo.Mcp.Services;
using OrderDemo.Mcp.Tools;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder  = WebApplication.CreateBuilder(args);
    var useStdio = args.Contains("--stdio");

    builder.Host.UseSerilog((context, services, config) =>
    {
        var logConfig = config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId();

        if (useStdio)
            logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
        else
            logConfig.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
    });

    builder.Services.AddHttpClient<ApiClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["ApiClient:BaseUrl"]!));

    builder.Services.AddHttpClient("health", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    });

    builder.Services.AddHealthChecks()
        .AddCheck<ApiHealthCheck>(
            name: "api",
            failureStatus: HealthStatus.Degraded,
            tags: ["api", "upstream"]);

    var mcpBuilder = builder.Services
        .AddMcpServer()
        .WithTools<OrderTools>()
        .WithPrompts<OrderPrompts>();

    if (useStdio)
        mcpBuilder.WithStdioServerTransport();
    else
        mcpBuilder.WithHttpTransport();

    var app = builder.Build();

    await app.Services.GetRequiredService<ApiClient>().InitializeAsync();

    if (!useStdio)
    {
        app.MapMcp();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });
    }

    await app.RunAsync();

    static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var result = new
        {
            status      = report.Status.ToString(),
            duration    = report.TotalDuration.TotalMilliseconds,
            checks      = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                duration    = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                error       = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(result,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCP server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
