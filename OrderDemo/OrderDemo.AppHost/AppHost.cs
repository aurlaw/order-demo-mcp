var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.OrderDemo_Api>("orderdemo-api", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("http", url => url.Url = "/scalar");

var mcp = builder.AddProject<Projects.OrderDemo_Mcp>("orderdemo-mcp", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5010, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api)
    .WithUrlForEndpoint("http", url => url.Url = "/mcp");

builder.AddJavaScriptApp("web", "../OrderDemo.Web", "dev")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithEnvironment("BROWSER", "none")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
