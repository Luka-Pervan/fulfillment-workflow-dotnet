using Fulfillment.Application;
using Fulfillment.Domain;
using Fulfillment.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Fulfillment.Api.IntegrationTests;

public sealed class PostgresPersistenceTests
{
    public const string ConnectionStringVariable =
        "POSTGRES_TEST_CONNECTION_STRING";

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task PostgreSql_enforces_idempotency_concurrency_and_atomic_outbox()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringVariable)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringVariable} is required.");
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var schemaContext = new FulfillmentDbContext(options))
        {
            Assert.Empty(
                await schemaContext.Database.GetPendingMigrationsAsync());
        }

        var now = new DateTimeOffset(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);
        var key = $"postgres-{Guid.NewGuid():N}";
        var command = new CreateFulfillmentCommand(
            $"ORDER-{Guid.NewGuid():N}",
            "HR",
            "21000");
        CreateFulfillmentResult created;

        await using (var createContext = new FulfillmentDbContext(options))
        {
            var service = new FulfillmentService(
                new EfFulfillmentStore(createContext),
                new FixedClock(now));
            created = await service.CreateAsync(
                key,
                command,
                CancellationToken.None);
        }

        await using (var verificationContext = new FulfillmentDbContext(options))
        {
            var stored = await verificationContext.FulfillmentRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == created.Request.Id);
            var outbox = await verificationContext.OutboxMessages
                .AsNoTracking()
                .SingleAsync(x => x.Type == "fulfillment.requested.v1"
                    && x.Payload.Contains(created.Request.Id.ToString()));

            Assert.Equal(
                IdempotencyRequestFingerprint.Compute(command),
                stored.RequestHash);
            Assert.Contains(created.Request.Id.ToString(), outbox.Payload);
        }

        await using (var duplicateContext = new FulfillmentDbContext(options))
        {
            duplicateContext.FulfillmentRequests.Add(
                FulfillmentRequest.Create(
                    key,
                    IdempotencyRequestFingerprint.Compute(
                        command with { DestinationPostalCode = "10000" }),
                    command.ExternalReference,
                    command.DestinationCountryCode,
                    "10000",
                    now));

            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicateContext.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(
                duplicate.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        }

        var raceKey = $"postgres-race-{Guid.NewGuid():N}";
        var raceCommand = new CreateFulfillmentCommand(
            $"ORDER-{Guid.NewGuid():N}",
            "HR",
            "21000");
        var coordinator = new LookupCoordinator();
        await using (var firstRaceContext = new FulfillmentDbContext(options))
        await using (var secondRaceContext = new FulfillmentDbContext(options))
        {
            var firstService = new FulfillmentService(
                new CoordinatedStore(
                    new EfFulfillmentStore(firstRaceContext),
                    coordinator),
                new FixedClock(now));
            var secondService = new FulfillmentService(
                new CoordinatedStore(
                    new EfFulfillmentStore(secondRaceContext),
                    coordinator),
                new FixedClock(now));

            var outcomes = await Task.WhenAll(
                firstService.CreateAsync(
                    raceKey,
                    raceCommand,
                    CancellationToken.None),
                secondService.CreateAsync(
                    raceKey,
                    raceCommand,
                    CancellationToken.None));

            Assert.Single(outcomes, result => !result.Replayed);
            Assert.Single(outcomes, result => result.Replayed);
            Assert.Equal(outcomes[0].Request.Id, outcomes[1].Request.Id);
        }

        await using (var raceVerification = new FulfillmentDbContext(options))
        {
            var racedRequests = await raceVerification.FulfillmentRequests
                .AsNoTracking()
                .Where(x => x.IdempotencyKey == raceKey)
                .ToArrayAsync();
            var racedRequest = Assert.Single(racedRequests);
            var racedOutbox = await raceVerification.OutboxMessages
                .AsNoTracking()
                .ToArrayAsync();
            Assert.Single(racedOutbox, message =>
                    message.Payload.Contains(racedRequest.Id.ToString()));
        }

        await using var firstWriter = new FulfillmentDbContext(options);
        await using var secondWriter = new FulfillmentDbContext(options);
        var firstCopy = await firstWriter.FulfillmentRequests
            .SingleAsync(x => x.Id == created.Request.Id);
        var secondCopy = await secondWriter.FulfillmentRequests
            .SingleAsync(x => x.Id == created.Request.Id);
        firstCopy.TransitionTo(
            FulfillmentStatus.Processing,
            now.AddMinutes(1));
        secondCopy.TransitionTo(
            FulfillmentStatus.Cancelled,
            now.AddMinutes(1));

        await firstWriter.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondWriter.SaveChangesAsync());
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class LookupCoordinator
    {
        private readonly TaskCompletionSource ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CoordinatedStore(
        IFulfillmentStore inner,
        LookupCoordinator coordinator)
        : IFulfillmentStore
    {
        public async Task<FulfillmentRequest?> FindByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var result = await inner.FindByIdempotencyKeyAsync(
                idempotencyKey,
                cancellationToken);
            await coordinator.ArriveAsync(cancellationToken);
            return result;
        }

        public Task<FulfillmentRequest?> FindByIdAsync(
            Guid id,
            bool forUpdate,
            CancellationToken cancellationToken) =>
            inner.FindByIdAsync(id, forUpdate, cancellationToken);

        public Task<PagedResult<FulfillmentRequest>> ListAsync(
            FulfillmentStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            inner.ListAsync(status, page, pageSize, cancellationToken);

        public void Add(FulfillmentRequest request) => inner.Add(request);

        public void Add(OutboxMessage message) => inner.Add(message);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            inner.SaveChangesAsync(cancellationToken);
    }
}
