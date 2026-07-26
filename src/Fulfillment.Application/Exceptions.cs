namespace Fulfillment.Application;

public sealed class FulfillmentNotFoundException(Guid id)
    : Exception($"Fulfillment request '{id}' was not found.")
{
    public Guid RequestId { get; } = id;
}

public sealed class FulfillmentConcurrencyException(
    Guid id,
    long expectedVersion,
    Exception? innerException = null)
    : Exception(
        $"Fulfillment request '{id}' changed after version {expectedVersion}.",
        innerException)
{
    public Guid RequestId { get; } = id;

    public long ExpectedVersion { get; } = expectedVersion;
}

public sealed class DuplicateIdempotencyKeyException(string key)
    : Exception($"Idempotency key '{key}' is already being processed.")
{
    public string Key { get; } = key;
}

public sealed class IdempotencyKeyPayloadMismatchException(string key)
    : Exception(
        $"Idempotency key '{key}' was already used for a different request payload.")
{
    public string Key { get; } = key;
}
