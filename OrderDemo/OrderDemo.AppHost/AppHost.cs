var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.OrderDemo_Api>("api", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5000, name: "http")
    .WithExternalHttpEndpoints();

var mcp = builder.AddProject<Projects.OrderDemo_Mcp>("mcp", launchProfileName: null)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithHttpEndpoint(port: 5010, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.AddViteApp("web", "../OrderDemo.Web")
    .WithEnvironment("BROWSER", "none")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
