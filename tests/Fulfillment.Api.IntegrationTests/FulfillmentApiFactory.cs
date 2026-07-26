using Fulfillment.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Fulfillment.Api.IntegrationTests;

public sealed class FulfillmentApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();

        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FulfillmentDbContext>>();
            services.RemoveAll<FulfillmentDbContext>();
            services.RemoveAll<IHostedService>();

            services.AddDbContext<FulfillmentDbContext>(options =>
                options.UseSqlite(connection));

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            connection.Dispose();
        }
    }
}
