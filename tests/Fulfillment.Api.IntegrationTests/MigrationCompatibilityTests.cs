using Fulfillment.Application;
using Fulfillment.Domain;
using Fulfillment.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fulfillment.Api.IntegrationTests;

public sealed class MigrationCompatibilityTests
{
    [Fact]
    public async Task Pre_hash_row_replays_safely_after_request_hash_migration()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FulfillmentDbContext>()
            .UseSqlite(connection)
            .Options;
        var id = Guid.NewGuid();
        var key = $"legacy-{Guid.NewGuid():N}";
        var now = new DateTimeOffset(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

        await using (var migrationContext = new FulfillmentDbContext(options))
        {
            await CreateHistoryWithSecondMigrationTemporarilyAppliedAsync(
                connection);
            await migrationContext.Database.MigrateAsync();
            await RemoveSecondMigrationHistoryAsync(connection);
            await InsertPreHashRowAsync(connection, id, key, now);
            await migrationContext.Database.MigrateAsync();

            var migratedHash = await migrationContext.FulfillmentRequests
                .AsNoTracking()
                .Where(request => request.Id == id)
                .Select(request => request.RequestHash)
                .SingleAsync();
            Assert.Equal(
                FulfillmentRequest.LegacyRequestHash,
                migratedHash);
        }

        await using var replayContext = new FulfillmentDbContext(options);
        var service = new FulfillmentService(
            new EfFulfillmentStore(replayContext),
            new FixedClock(now));
        var replay = await service.CreateAsync(
            key,
            new CreateFulfillmentCommand(
                "ORDER-LEGACY",
                "HR",
                "21000"),
            CancellationToken.None);

        Assert.True(replay.Replayed);
        Assert.Equal(id, replay.Request.Id);
        await Assert.ThrowsAsync<IdempotencyKeyPayloadMismatchException>(
            () => service.CreateAsync(
                key,
                new CreateFulfillmentCommand(
                    "ORDER-LEGACY",
                    "HR",
                    "10000"),
                CancellationToken.None));
    }

    private static async Task CreateHistoryWithSecondMigrationTemporarilyAppliedAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('202607260002_AddRequestHash', '10.0.10');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RemoveSecondMigrationHistoryAsync(
        SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '202607260002_AddRequestHash';
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertPreHashRowAsync(
        SqliteConnection connection,
        Guid id,
        string key,
        DateTimeOffset now)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO fulfillment_requests (
                "Id",
                "IdempotencyKey",
                "ExternalReference",
                "DestinationCountryCode",
                "DestinationPostalCode",
                "Status",
                "CreatedAt",
                "CreatedAtUnixMilliseconds",
                "UpdatedAt",
                "Version")
            VALUES (
                $id,
                $key,
                'ORDER-LEGACY',
                'HR',
                '21000',
                'Submitted',
                $createdAt,
                $createdAtUnixMilliseconds,
                $updatedAt,
                1);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue(
            "$createdAtUnixMilliseconds",
            now.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$updatedAt", now);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }
}
