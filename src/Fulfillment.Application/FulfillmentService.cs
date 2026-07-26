using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fulfillment.Domain;

namespace Fulfillment.Application;

public sealed class FulfillmentService(
    IFulfillmentStore store,
    IClock clock)
{
    public async Task<CreateFulfillmentResult> CreateAsync(
        string idempotencyKey,
        CreateFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        var requestHash = IdempotencyRequestFingerprint.Compute(command);

        var existing = await store.FindByIdempotencyKeyAsync(
            normalizedIdempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            return ReplayOrThrow(
                existing,
                normalizedIdempotencyKey,
                requestHash);
        }

        var now = clock.UtcNow;
        var request = FulfillmentRequest.Create(
            normalizedIdempotencyKey,
            requestHash,
            command.ExternalReference,
            command.DestinationCountryCode,
            command.DestinationPostalCode,
            now);

        store.Add(request);
        store.Add(CreateEvent(
            "fulfillment.requested.v1",
            request,
            now));

        try
        {
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateIdempotencyKeyException)
        {
            var winner = await store.FindByIdempotencyKeyAsync(
                normalizedIdempotencyKey,
                cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return ReplayOrThrow(
                winner,
                normalizedIdempotencyKey,
                requestHash);
        }

        return new CreateFulfillmentResult(Map(request), Replayed: false);
    }

    public async Task<FulfillmentResponse> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var request = await store.FindByIdAsync(id, forUpdate: false, cancellationToken)
            ?? throw new FulfillmentNotFoundException(id);

        return Map(request);
    }

    public async Task<PagedResult<FulfillmentResponse>> ListAsync(
        FulfillmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Page size must be between 1 and 100.");
        }

        var result = await store.ListAsync(status, page, pageSize, cancellationToken);

        return new PagedResult<FulfillmentResponse>(
            result.Items.Select(Map).ToArray(),
            result.Page,
            result.PageSize,
            result.Total);
    }

    public async Task<FulfillmentResponse> TransitionAsync(
        Guid id,
        TransitionFulfillmentCommand command,
        CancellationToken cancellationToken)
    {
        var request = await store.FindByIdAsync(id, forUpdate: true, cancellationToken)
            ?? throw new FulfillmentNotFoundException(id);

        if (request.Version != command.ExpectedVersion)
        {
            throw new FulfillmentConcurrencyException(id, command.ExpectedVersion);
        }

        var now = clock.UtcNow;
        request.TransitionTo(command.Status, now);
        store.Add(CreateEvent(
            "fulfillment.status-changed.v1",
            request,
            now));

        await store.SaveChangesAsync(cancellationToken);

        return Map(request);
    }

    private static string NormalizeIdempotencyKey(string key)
    {
        var normalized = key?.Trim() ?? string.Empty;
        if (normalized.Length is < 8 or > 128)
        {
            throw new ArgumentException(
                "Idempotency-Key must contain between 8 and 128 characters.",
                nameof(key));
        }

        return normalized;
    }

    private static CreateFulfillmentResult ReplayOrThrow(
        FulfillmentRequest existing,
        string idempotencyKey,
        string requestHash)
    {
        var storedHash = existing.RequestHash == FulfillmentRequest.LegacyRequestHash
            ? IdempotencyRequestFingerprint.Compute(
                new CreateFulfillmentCommand(
                    existing.ExternalReference,
                    existing.DestinationCountryCode,
                    existing.DestinationPostalCode))
            : existing.RequestHash;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(storedHash),
                Encoding.ASCII.GetBytes(requestHash)))
        {
            throw new IdempotencyKeyPayloadMismatchException(idempotencyKey);
        }

        return new CreateFulfillmentResult(Map(existing), Replayed: true);
    }

    private static OutboxMessage CreateEvent(
        string type,
        FulfillmentRequest request,
        DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.Id,
            request.ExternalReference,
            Status = request.Status.ToString(),
            request.Version,
            OccurredAt = occurredAt,
        });

        return OutboxMessage.Create(type, payload, occurredAt);
    }

    private static FulfillmentResponse Map(FulfillmentRequest request) =>
        new(
            request.Id,
            request.ExternalReference,
            request.DestinationCountryCode,
            request.DestinationPostalCode,
            request.Status,
            request.CreatedAt,
            request.UpdatedAt,
            request.Version);
}
