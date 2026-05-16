using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OrderDemo.Api.DTOs;

namespace OrderDemo.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/auth/login", async (
            LoginRequest request,
            UserManager<IdentityUser> userManager,
            IConfiguration config) =>
        {
            var user = await userManager.FindByNameAsync(request.Username);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            var token     = GenerateToken(user, config);
            var expiresAt = DateTime.UtcNow.AddHours(8);

            return Results.Ok(new LoginResponse(token, expiresAt));
        })
        .WithName("Login")
        .WithSummary("Authenticate and receive a JWT token");
    }

    private static string GenerateToken(IdentityUser user, IConfiguration config)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        user.Id),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
