using Fulfillment.Domain;

namespace Fulfillment.Application;

public sealed record CreateFulfillmentCommand(
    string ExternalReference,
    string DestinationCountryCode,
    string DestinationPostalCode);

public sealed record TransitionFulfillmentCommand(
    FulfillmentStatus Status,
    long ExpectedVersion);

public sealed record FulfillmentResponse(
    Guid Id,
    string ExternalReference,
    string DestinationCountryCode,
    string DestinationPostalCode,
    FulfillmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    long Version);

public sealed record CreateFulfillmentResult(
    FulfillmentResponse Request,
    bool Replayed);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);
