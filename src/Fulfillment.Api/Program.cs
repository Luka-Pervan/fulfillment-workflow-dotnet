using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Fulfillment.Api;
using Fulfillment.Application;
using Fulfillment.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
var rateLimitPermitLimit = builder.Configuration.GetValue(
    "RateLimiting:PermitLimit",
    100);
var rateLimitWindowSeconds = builder.Configuration.GetValue(
    "RateLimiting:WindowSeconds",
    60);
if (rateLimitPermitLimit <= 0 || rateLimitWindowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting:PermitLimit and RateLimiting:WindowSeconds "
        + "must both be greater than zero.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (rejected, cancellationToken) =>
    {
        var retryAfter = rejected.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var retry)
            ? retry
            : TimeSpan.FromSeconds(rateLimitWindowSeconds);
        var retryAfterSeconds = Math.Max(
            1,
            (int)Math.Ceiling(retryAfter.TotalSeconds));

        rejected.HttpContext.Response.Headers.RetryAfter =
            retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        rejected.HttpContext.Response.StatusCode =
            StatusCodes.Status429TooManyRequests;
        Telemetry.RecordRateLimitRejection();

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Rate limit exceeded",
            Detail = "Too many requests were received. Retry after the indicated delay.",
            Instance = rejected.HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = rejected.HttpContext.TraceIdentifier,
                ["retryAfterSeconds"] = retryAfterSeconds,
            },
        };

        var service = rejected.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        if (!await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = rejected.HttpContext,
            ProblemDetails = problemDetails,
        }))
        {
            rejected.HttpContext.Response.ContentType =
                "application/problem+json";
            await rejected.HttpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);
        }
    };
    options.AddPolicy("api", httpContext =>
    {
        var subject = httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? httpContext.User.FindFirstValue("sub");
        var partitionKey = !string.IsNullOrWhiteSpace(subject)
            ? $"subject:{subject}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

var authentication = builder.Configuration.GetSection("Authentication");
var signingKey = authentication["SigningKey"];
var issuer = authentication["Issuer"];
var authority = authentication["Authority"];
var audience = authentication["Audience"];
var isTesting = builder.Environment.IsEnvironment("Testing");

if (builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(signingKey)
        || signingKey.Length < 32
        || string.IsNullOrWhiteSpace(issuer)
        || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException(
            "Development authentication requires a signing key of at least "
            + "32 characters plus Authentication:Issuer and "
            + "Authentication:Audience.");
    }
}
else if (!isTesting
         && (string.IsNullOrWhiteSpace(authority)
             || string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException(
        "Non-development authentication requires both "
        + "Authentication:Authority and Authentication:Audience.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(signingKey!)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }
        else if (!isTesting)
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = true;
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("fulfillment.read", policy =>
        policy.RequireRole("Operator", "Supervisor"))
    .AddPolicy("fulfillment.write", policy =>
        policy.RequireRole("Operator", "Supervisor"))
    .AddPolicy("fulfillment.transition", policy =>
        policy.RequireRole("Supervisor"));

var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Fulfillment")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Fulfillment must be configured.");

builder.Services.AddDbContext<FulfillmentDbContext>(options =>
{
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        throw new InvalidOperationException(
            $"Unsupported database provider '{provider}'.");
    }
});

builder.Services.AddScoped<IFulfillmentStore, EfFulfillmentStore>();
builder.Services.AddScoped<FulfillmentService>();
builder.Services.AddScoped<IEventSink, LoggingEventSink>();
builder.Services.AddScoped<OutboxDispatcher>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddHostedService<OutboxPublisher>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FulfillmentDbContext>(
        name: "database",
        tags: ["ready"]);

var telemetry = builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("fulfillment-api"));

telemetry.WithTracing(tracing =>
{
    tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(Telemetry.ActivitySourceName);

    if (!string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    {
        tracing.AddOtlpExporter();
    }
});

telemetry.WithMetrics(metrics =>
{
    metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(Telemetry.MeterName);

    if (!string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    {
        metrics.AddOtlpExporter();
    }
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; base-uri 'none'; object-src 'none'; "
            + "frame-ancestors 'none'; form-action 'self'; "
            + "connect-src 'self'; img-src 'self' data:; "
            + "script-src 'self'; style-src 'self'";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Task.CompletedTask;
    });

    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapDevelopmentTokenEndpoint();
}

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapFulfillmentEndpoints();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
    await dbContext.Database.MigrateAsync();
    return;
}

if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        throw new InvalidOperationException(
            "Database:MigrateOnStartup is restricted to Development and Testing. "
            + "Run the explicit '--migrate' deployment command before starting "
            + "the production API.");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
