using Fulfillment.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fulfillment.Infrastructure;

/// <summary>
/// Dispatches one bounded batch of pending outbox messages.
/// </summary>
public sealed class OutboxDispatcher(
    FulfillmentDbContext dbContext,
    IEventSink sink,
    IClock clock,
    ILogger<OutboxDispatcher> logger)
{
    private const int MaximumAttempts = 10;
    private const int BatchSize = 20;

    /// <summary>
    /// Attempts each eligible message once and persists the resulting delivery
    /// state. Failed messages remain eligible for a later invocation until the
    /// bounded attempt limit is reached.
    /// </summary>
    public async Task<int> DispatchPendingAsync(
        CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .Where(x => x.PublishedAt == null && x.Attempts < MaximumAttempts)
            .OrderBy(x => x.OccurredAtUnixMilliseconds)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await sink.PublishAsync(message, cancellationToken);
                message.MarkPublished(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var errorCode = exception.GetType().Name;
                message.MarkFailed(errorCode);
                logger.LogWarning(
                    "Publishing outbox event {EventId} failed on attempt {Attempt}. "
                    + "Error type: {ErrorType}.",
                    message.Id,
                    message.Attempts,
                    errorCode);
            }
        }

        if (messages.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return messages.Length;
    }
}
