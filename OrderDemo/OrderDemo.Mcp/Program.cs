using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
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
    var useStdio = args.Contains("--stdio");

    builder.Configuration["UseStdio"] = useStdio.ToString();

    builder.AddServiceDefaults();

    builder.Host.UseSerilog((context, services, config) =>
    {
        var logConfig = config
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
            logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose);
    });

    builder.Services.AddHttpClient<ApiClient>(client =>
        client.BaseAddress = new Uri("http://orderdemo-api"))
        .AddServiceDiscovery()
        .AddStandardResilienceHandler();

    builder.Services.AddHttpClient("health", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddServiceDiscovery()
    .AddStandardResilienceHandler();

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

    await app.Services.GetRequiredService<ApiClient>().InitializeAsync();

    if (!useStdio)
    {
        app.MapMcp("/mcp");
        app.MapDefaultEndpoints();
    }

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
