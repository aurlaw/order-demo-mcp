using ModelContextProtocol.AspNetCore;
using OrderDemo.Mcp.Prompts;
using OrderDemo.Mcp.Services;
using OrderDemo.Mcp.Tools;

var builder  = WebApplication.CreateBuilder(args);
var useStdio = args.Contains("--stdio");

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
