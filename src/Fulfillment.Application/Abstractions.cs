using Fulfillment.Domain;

namespace Fulfillment.Application;

public interface IFulfillmentStore
{
    Task<FulfillmentRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<FulfillmentRequest?> FindByIdAsync(
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken);

    Task<PagedResult<FulfillmentRequest>> ListAsync(
        FulfillmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    void Add(FulfillmentRequest request);

    void Add(OutboxMessage message);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IEventSink
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
