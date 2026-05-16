using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderDemo.Api.Data;
using OrderDemo.Api.Endpoints;
using OrderDemo.Api.HealthChecks;
using OrderDemo.Api.Middleware;
using OrderDemo.Api.Services;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();

    builder.Host.UseSerilog((context, services, config) =>
    {
        config
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
    });

    builder.Services.AddOpenApi();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped<OrderService>();

    builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit           = true;
        options.Password.RequiredLength         = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase       = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.AddAuthentication()
        .AddJwtBearer("Local", options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = builder.Configuration["Jwt:Issuer"],
                ValidAudience            = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
            };
        })
        .AddJwtBearer("Auth0", options =>
        {
            options.Authority = $"https://{builder.Configuration["Auth0:Domain"]}/";
            options.Audience  = builder.Configuration["Auth0:Audience"];
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ApiAccess", policy =>
        {
            policy.AddAuthenticationSchemes("Local", "Auth0");
            policy.RequireAuthenticatedUser();
        });
    });

    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>(
            name: "database",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "sqlite"]);

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        db.Database.EnsureCreated();
        await Seeder.SeedAsync(db, userManager, config);
    }

    app.MapOpenApi();
    app.MapScalarApiReference(opts => opts.WithTitle("Order Demo API"));
    app.UseCors();

    app.UseMiddleware<CorrelationMiddleware>();
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
        {
            diagCtx.Set("RequestHost",   httpCtx.Request.Host.Value);
            diagCtx.Set("RequestScheme", httpCtx.Request.Scheme);
            diagCtx.Set("UserAgent",     httpCtx.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapAuthEndpoints();
    app.MapOrderEndpoints();

    app.MapDefaultEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
