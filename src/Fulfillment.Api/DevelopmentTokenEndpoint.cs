using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Fulfillment.Api;

public static class DevelopmentTokenEndpoint
{
    public static IEndpointRouteBuilder MapDevelopmentTokenEndpoint(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/dev/token", (
            DevelopmentTokenRequest request,
            IConfiguration configuration,
            IHostEnvironment environment) =>
        {
            if (!environment.IsDevelopment())
            {
                return Results.NotFound();
            }

            if (request.Role is not ("Operator" or "Supervisor"))
            {
                return Results.BadRequest(new
                {
                    error = "Role must be Operator or Supervisor.",
                });
            }

            var key = configuration["Authentication:SigningKey"];
            if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            {
                return Results.Problem(
                    title: "Development signing key is not configured.",
                    detail: "Set Authentication__SigningKey to at least 32 characters.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var issuer = configuration["Authentication:Issuer"];
            var audience = configuration["Authentication:Audience"];
            var expires = DateTime.UtcNow.AddMinutes(30);
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, "local-developer"),
                    new Claim(ClaimTypes.Name, "Local Developer"),
                    new Claim(ClaimTypes.Role, request.Role),
                ]),
                Expires = expires,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    SecurityAlgorithms.HmacSha256),
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);

            return Results.Ok(new
            {
                accessToken = handler.WriteToken(token),
                expiresAt = expires,
            });
        }).ExcludeFromDescription();

        return endpoints;
    }
}

public sealed record DevelopmentTokenRequest(string Role);
