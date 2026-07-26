using Fulfillment.Application;
using Fulfillment.Domain;
using Microsoft.Extensions.Logging;

namespace Fulfillment.Infrastructure;

public sealed class LoggingEventSink(ILogger<LoggingEventSink> logger) : IEventSink
{
    public Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Published outbox event {EventId} of type {EventType}.",
            message.Id,
            message.Type);

        return Task.CompletedTask;
    }
}
