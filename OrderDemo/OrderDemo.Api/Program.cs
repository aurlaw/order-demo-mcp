using Microsoft.EntityFrameworkCore;
using OrderDemo.Api.Data;
using OrderDemo.Api.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    Seeder.Seed(db);
}

app.MapOpenApi();
app.MapScalarApiReference(opts => opts.WithTitle("Order Demo API"));
app.UseCors();
app.MapOrderEndpoints();
app.Run();
