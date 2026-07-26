using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fulfillment.Api.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RoleHeader = "X-Test-Role";
    public const string SubjectHeader = "X-Test-Subject";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers[RoleHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = Request.Headers[SubjectHeader].FirstOrDefault()
            ?? "integration-test";
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim(ClaimTypes.Name, "Integration Test"),
            new Claim(ClaimTypes.Role, role),
        ],
        SchemeName);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
