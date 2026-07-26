namespace Fulfillment.Domain;

public sealed class FulfillmentRequest
{
    public const string LegacyRequestHash = "legacy-unavailable";

    private FulfillmentRequest()
    {
    }

    private FulfillmentRequest(
        Guid id,
        string idempotencyKey,
        string requestHash,
        string externalReference,
        string destinationCountryCode,
        string destinationPostalCode,
        DateTimeOffset createdAt)
    {
        Id = id;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        ExternalReference = externalReference;
        DestinationCountryCode = destinationCountryCode;
        DestinationPostalCode = destinationPostalCode;
        Status = FulfillmentStatus.Submitted;
        CreatedAt = createdAt;
        CreatedAtUnixMilliseconds = createdAt.ToUnixTimeMilliseconds();
        UpdatedAt = createdAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public string ExternalReference { get; private set; } = string.Empty;

    public string DestinationCountryCode { get; private set; } = string.Empty;

    public string DestinationPostalCode { get; private set; } = string.Empty;

    public FulfillmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public long CreatedAtUnixMilliseconds { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Version { get; private set; }

    public static FulfillmentRequest Create(
        string idempotencyKey,
        string requestHash,
        string externalReference,
        string destinationCountryCode,
        string destinationPostalCode,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationCountryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPostalCode);

        var normalizedCountryCode = destinationCountryCode.Trim();
        if (normalizedCountryCode.Length != 2
            || !normalizedCountryCode.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "Destination country code must contain two ASCII letters.",
                nameof(destinationCountryCode));
        }

        return new FulfillmentRequest(
            Guid.NewGuid(),
            idempotencyKey.Trim(),
            requestHash,
            externalReference.Trim(),
            normalizedCountryCode.ToUpperInvariant(),
            destinationPostalCode.Trim(),
            now);
    }

    public void TransitionTo(FulfillmentStatus requested, DateTimeOffset now)
    {
        var allowed = Status switch
        {
            FulfillmentStatus.Submitted =>
                requested is FulfillmentStatus.Processing or FulfillmentStatus.Cancelled,
            FulfillmentStatus.Processing =>
                requested is FulfillmentStatus.Completed or FulfillmentStatus.Cancelled,
            FulfillmentStatus.Completed or FulfillmentStatus.Cancelled => false,
            _ => false,
        };

        if (!allowed)
        {
            throw new InvalidStateTransitionException(Status, requested);
        }

        Status = requested;
        UpdatedAt = now;
        Version++;
    }
}
