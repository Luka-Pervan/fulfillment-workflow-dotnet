using System.Diagnostics;

namespace Fulfillment.Api;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        {
            correlationId = Activity.Current?.TraceId.ToString()
                ?? Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
