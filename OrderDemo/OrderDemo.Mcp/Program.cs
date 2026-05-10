using ModelContextProtocol.AspNetCore;
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
        app.MapMcp();

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
