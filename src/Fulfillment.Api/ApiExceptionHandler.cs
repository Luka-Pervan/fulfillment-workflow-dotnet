using Fulfillment.Application;
using Fulfillment.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fulfillment.Api;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            FulfillmentNotFoundException =>
                (StatusCodes.Status404NotFound, "Request not found", exception.Message),
            IdempotencyKeyPayloadMismatchException =>
                (StatusCodes.Status409Conflict, "Idempotency key conflict", exception.Message),
            FulfillmentConcurrencyException or DuplicateIdempotencyKeyException =>
                (StatusCodes.Status409Conflict, "Concurrency conflict", exception.Message),
            InvalidStateTransitionException =>
                (StatusCodes.Status422UnprocessableEntity, "Invalid state transition", exception.Message),
            BadHttpRequestException or ArgumentException or ArgumentOutOfRangeException =>
                (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Path}.", httpContext.Request.Path);
        }
        else if (exception is IdempotencyKeyPayloadMismatchException)
        {
            Telemetry.RecordIdempotencyConflict();
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = httpContext.TraceIdentifier,
                },
            },
        });
    }
}
