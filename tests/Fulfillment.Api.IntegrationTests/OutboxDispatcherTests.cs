using Fulfillment.Application;
using Fulfillment.Domain;
using Fulfillment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fulfillment.Api.IntegrationTests;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task Failed_delivery_is_recorded_without_message_and_retried()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseSqlite(connection)
            .Options;
        var occurredAt = new DateTimeOffset(
            2026,
            7,
            27,
            8,
            0,
            0,
            TimeSpan.Zero);
        var message = OutboxMessage.Create(
            "fulfillment.requested.v1",
            """{"requestId":"synthetic"}""",
            occurredAt);
        var sink = new FailOnceEventSink();

        await using (var setupContext = new FulfillmentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.OutboxMessages.Add(message);
            await setupContext.SaveChangesAsync();
        }

        await using (var firstAttemptContext = new FulfillmentDbContext(options))
        {
            var dispatcher = CreateDispatcher(
                firstAttemptContext,
                sink,
                occurredAt.AddSeconds(1));

            Assert.Equal(
                1,
                await dispatcher.DispatchPendingAsync(CancellationToken.None));
        }

        await using (var failedContext = new FulfillmentDbContext(options))
        {
            var failed = await failedContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync();

            Assert.Null(failed.PublishedAt);
            Assert.Equal(1, failed.Attempts);
            Assert.Equal(nameof(InvalidOperationException), failed.LastError);
            Assert.NotNull(failed.LastError);
            Assert.DoesNotContain(FailOnceEventSink.SensitiveMessage, failed.LastError);
        }

        var publishedAt = occurredAt.AddSeconds(2);
        await using (var retryContext = new FulfillmentDbContext(options))
        {
            var dispatcher = CreateDispatcher(retryContext, sink, publishedAt);

            Assert.Equal(
                1,
                await dispatcher.DispatchPendingAsync(CancellationToken.None));
        }

        await using var verificationContext = new FulfillmentDbContext(options);
        var published = await verificationContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(publishedAt, published.PublishedAt);
        Assert.Equal(2, published.Attempts);
        Assert.Null(published.LastError);
        Assert.Equal(2, sink.Attempts);
    }

    [Fact]
    public async Task Messages_at_the_attempt_limit_are_not_selected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseSqlite(connection)
            .Options;
        var message = OutboxMessage.Create(
            "fulfillment.requested.v1",
            """{"requestId":"attempt-limit"}""",
            DateTimeOffset.UtcNow);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            message.MarkFailed(nameof(InvalidOperationException));
        }

        await using var dbContext = new FulfillmentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();
        var sink = new RecordingEventSink();
        var dispatcher = CreateDispatcher(
            dbContext,
            sink,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            0,
            await dispatcher.DispatchPendingAsync(CancellationToken.None));
        Assert.Equal(0, sink.Attempts);
    }

    [Fact]
    public async Task One_dispatch_processes_at_most_twenty_messages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseSqlite(connection)
            .Options;
        var occurredAt = DateTimeOffset.UtcNow;

        await using var dbContext = new FulfillmentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.OutboxMessages.AddRange(
            Enumerable.Range(0, 21).Select(index =>
                OutboxMessage.Create(
                    "fulfillment.requested.v1",
                    $"{{\"requestId\":\"batch-{index}\"}}",
                    occurredAt.AddMilliseconds(index))));
        await dbContext.SaveChangesAsync();
        var sink = new RecordingEventSink();
        var dispatcher = CreateDispatcher(
            dbContext,
            sink,
            occurredAt.AddMinutes(1));

        Assert.Equal(
            20,
            await dispatcher.DispatchPendingAsync(CancellationToken.None));
        Assert.Equal(20, sink.Attempts);
        Assert.Equal(
            1,
            await dbContext.OutboxMessages.CountAsync(
                message => message.PublishedAt == null));
    }

    private static OutboxDispatcher CreateDispatcher(
        FulfillmentDbContext dbContext,
        IEventSink sink,
        DateTimeOffset now) =>
        new(
            dbContext,
            sink,
            new FixedClock(now),
            NullLogger<OutboxDispatcher>.Instance);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FailOnceEventSink : IEventSink
    {
        public const string SensitiveMessage =
            "provider-secret=do-not-persist";

        public int Attempts { get; private set; }

        public Task PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            Attempts++;
            if (Attempts == 1)
            {
                throw new InvalidOperationException(SensitiveMessage);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventSink : IEventSink
    {
        public int Attempts { get; private set; }

        public Task PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            Attempts++;
            return Task.CompletedTask;
        }
    }
}
