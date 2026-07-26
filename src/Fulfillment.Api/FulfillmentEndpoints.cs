using System.Diagnostics;
using System.Diagnostics.Metrics;
using Fulfillment.Application;
using Fulfillment.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Fulfillment.Api;

public static class FulfillmentEndpoints
{
    public static IEndpointRouteBuilder MapFulfillmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/fulfillment-requests")
            .RequireRateLimiting("api")
            .WithTags("Fulfillment");

        group.MapPost("/", CreateAsync)
            .RequireAuthorization("fulfillment.write");

        group.MapGet("/", ListAsync)
            .RequireAuthorization("fulfillment.read");

        group.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization("fulfillment.read");

        group.MapPost("/{id:guid}/transitions", TransitionAsync)
            .RequireAuthorization("fulfillment.transition");

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        HttpRequest httpRequest,
        [FromBody] CreateFulfillmentApiRequest request,
        FulfillmentService service,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
        var result = await service.CreateAsync(
            idempotencyKey,
            new CreateFulfillmentCommand(
                request.ExternalReference,
                request.DestinationCountryCode,
                request.DestinationPostalCode),
            cancellationToken);

        if (result.Replayed)
        {
            Telemetry.RecordCreate(replayed: true);
            return Results.Ok(result.Request);
        }

        Telemetry.RecordCreate(replayed: false);
        return Results.Created(
            $"/api/fulfillment-requests/{result.Request.Id}",
            result.Request);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        FulfillmentService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAsync(id, cancellationToken));

    private static async Task<IResult> ListAsync(
        [FromQuery] FulfillmentStatus? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        FulfillmentService service,
        CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? 20;

        return Results.Ok(
            await service.ListAsync(
                status,
                resolvedPage,
                resolvedPageSize,
                cancellationToken));
    }

    private static async Task<IResult> TransitionAsync(
        Guid id,
        [FromBody] TransitionFulfillmentApiRequest request,
        FulfillmentService service,
        CancellationToken cancellationToken)
    {
        if (request.ExpectedVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "ExpectedVersion must be at least 1.");
        }

        return Results.Ok(
            await service.TransitionAsync(
                id,
                new TransitionFulfillmentCommand(
                    request.Status,
                    request.ExpectedVersion),
                cancellationToken));
    }

    private static void Validate(CreateFulfillmentApiRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalReference)
            || request.ExternalReference.Length > 100)
        {
            throw new ArgumentException(
                "ExternalReference is required and may contain at most 100 characters.");
        }

        var countryCode = request.DestinationCountryCode?.Trim();
        if (countryCode is not { Length: 2 }
            || !countryCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "DestinationCountryCode must contain a two-letter ASCII country code.");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationPostalCode)
            || request.DestinationPostalCode.Length > 20)
        {
            throw new ArgumentException(
                "DestinationPostalCode is required and may contain at most 20 characters.");
        }
    }
}

public sealed record CreateFulfillmentApiRequest(
    string ExternalReference,
    string DestinationCountryCode,
    string DestinationPostalCode);

public sealed record TransitionFulfillmentApiRequest(
    FulfillmentStatus Status,
    long ExpectedVersion);

public static class Telemetry
{
    public const string ActivitySourceName = "Fulfillment.Api";

    public const string MeterName = "Fulfillment.Api";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Creates = Meter.CreateCounter<long>(
        "fulfillment.create.requests",
        description: "Fulfillment create requests by bounded outcome.");
    private static readonly Counter<long> IdempotencyConflicts =
        Meter.CreateCounter<long>(
            "fulfillment.idempotency.conflicts",
            description: "Create attempts that reused a key with a different payload.");
    private static readonly Counter<long> RateLimitRejections =
        Meter.CreateCounter<long>(
            "fulfillment.rate_limit.rejections",
            description: "Requests rejected by the API rate limiter.");

    /// <summary>
    /// Records a create result using a bounded outcome value.
    /// </summary>
    public static void RecordCreate(bool replayed) =>
        Creates.Add(1, new KeyValuePair<string, object?>(
            "outcome",
            replayed ? "replayed" : "created"));

    /// <summary>
    /// Records an idempotency-key reuse with different request semantics.
    /// </summary>
    public static void RecordIdempotencyConflict() =>
        IdempotencyConflicts.Add(1);

    /// <summary>
    /// Records a request rejected at the rate-limit boundary.
    /// </summary>
    public static void RecordRateLimitRejection() =>
        RateLimitRejections.Add(1);
}
