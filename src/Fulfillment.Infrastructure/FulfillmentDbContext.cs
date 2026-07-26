using Fulfillment.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fulfillment.Infrastructure;

public sealed class FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options)
    : DbContext(options)
{
    public DbSet<FulfillmentRequest> FulfillmentRequests => Set<FulfillmentRequest>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var fulfillment = modelBuilder.Entity<FulfillmentRequest>();
        fulfillment.ToTable("fulfillment_requests");
        fulfillment.HasKey(x => x.Id);
        fulfillment.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        fulfillment.HasIndex(x => x.IdempotencyKey).IsUnique();
        fulfillment.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        fulfillment.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
        fulfillment.Property(x => x.DestinationCountryCode).HasMaxLength(2).IsRequired();
        fulfillment.Property(x => x.DestinationPostalCode).HasMaxLength(20).IsRequired();
        fulfillment.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        fulfillment.Property(x => x.Version).IsConcurrencyToken();
        fulfillment.HasIndex(x => new { x.Status, x.CreatedAtUnixMilliseconds });

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox_messages");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Type).HasMaxLength(200).IsRequired();
        outbox.Property(x => x.Payload).IsRequired();
        outbox.Property(x => x.LastError).HasMaxLength(1_000);
        outbox.HasIndex(x => new { x.PublishedAt, x.OccurredAtUnixMilliseconds });
    }
}
