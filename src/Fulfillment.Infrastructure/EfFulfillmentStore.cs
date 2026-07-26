using Fulfillment.Application;
using Fulfillment.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fulfillment.Infrastructure;

public sealed class EfFulfillmentStore(FulfillmentDbContext dbContext)
    : IFulfillmentStore
{
    public Task<FulfillmentRequest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.FulfillmentRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<FulfillmentRequest?> FindByIdAsync(
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        IQueryable<FulfillmentRequest> query = dbContext.FulfillmentRequests;
        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PagedResult<FulfillmentRequest>> ListAsync(
        FulfillmentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FulfillmentRequests.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUnixMilliseconds)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResult<FulfillmentRequest>(items, page, pageSize, total);
    }

    public void Add(FulfillmentRequest request) =>
        dbContext.FulfillmentRequests.Add(request);

    public void Add(OutboxMessage message) =>
        dbContext.OutboxMessages.Add(message);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var request = exception.Entries
                .Select(x => x.Entity)
                .OfType<FulfillmentRequest>()
                .FirstOrDefault();

            throw new FulfillmentConcurrencyException(
                request?.Id ?? Guid.Empty,
                request?.Version - 1 ?? 0,
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            var request = exception.Entries
                .Select(x => x.Entity)
                .OfType<FulfillmentRequest>()
                .FirstOrDefault();
            var key = request?.IdempotencyKey ?? "unknown";

            dbContext.ChangeTracker.Clear();
            throw new DuplicateIdempotencyKeyException(key);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        || exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
