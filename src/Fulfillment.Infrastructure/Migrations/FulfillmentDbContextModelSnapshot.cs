using Fulfillment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace Fulfillment.Infrastructure.Migrations;

[DbContext(typeof(FulfillmentDbContext))]
public partial class FulfillmentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.10");

        modelBuilder.Entity<FulfillmentRequest>(entity =>
        {
            entity.Property<Guid>("Id");
            entity.Property<DateTimeOffset>("CreatedAt");
            entity.Property<long>("CreatedAtUnixMilliseconds");
            entity.Property<string>("DestinationCountryCode").IsRequired().HasMaxLength(2);
            entity.Property<string>("DestinationPostalCode").IsRequired().HasMaxLength(20);
            entity.Property<string>("ExternalReference").IsRequired().HasMaxLength(100);
            entity.Property<string>("IdempotencyKey").IsRequired().HasMaxLength(128);
            entity.Property<string>("RequestHash").IsRequired().HasMaxLength(64);
            entity.Property<FulfillmentStatus>("Status")
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property<DateTimeOffset>("UpdatedAt");
            entity.Property<long>("Version").IsConcurrencyToken();
            entity.HasKey("Id");
            entity.HasIndex("IdempotencyKey").IsUnique();
            entity.HasIndex("Status", "CreatedAtUnixMilliseconds");
            entity.ToTable("fulfillment_requests");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.Property<Guid>("Id");
            entity.Property<int>("Attempts");
            entity.Property<string>("LastError").HasMaxLength(1_000);
            entity.Property<DateTimeOffset>("OccurredAt");
            entity.Property<long>("OccurredAtUnixMilliseconds");
            entity.Property<string>("Payload").IsRequired();
            entity.Property<DateTimeOffset?>("PublishedAt");
            entity.Property<string>("Type").IsRequired().HasMaxLength(200);
            entity.HasKey("Id");
            entity.HasIndex("PublishedAt", "OccurredAtUnixMilliseconds");
            entity.ToTable("outbox_messages");
        });
    }
}
