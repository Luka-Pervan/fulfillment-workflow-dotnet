namespace Fulfillment.Domain;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        string type,
        string payload,
        DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
        OccurredAtUnixMilliseconds = occurredAt.ToUnixTimeMilliseconds();
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public long OccurredAtUnixMilliseconds { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        string type,
        string payload,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage(Guid.NewGuid(), type, payload, occurredAt);
    }

    public void MarkPublished(DateTimeOffset now)
    {
        PublishedAt = now;
        Attempts++;
        LastError = null;
    }

    public void MarkFailed(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        Attempts++;
        LastError = errorCode.Length <= 1_000
            ? errorCode
            : errorCode[..1_000];
    }
}
