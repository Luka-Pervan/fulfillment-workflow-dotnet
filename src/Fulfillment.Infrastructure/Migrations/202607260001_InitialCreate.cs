using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fulfillment.Infrastructure.Migrations;

[DbContext(typeof(FulfillmentDbContext))]
[Migration("202607260001_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "fulfillment_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                IdempotencyKey = table.Column<string>(maxLength: 128, nullable: false),
                ExternalReference = table.Column<string>(maxLength: 100, nullable: false),
                DestinationCountryCode = table.Column<string>(maxLength: 2, nullable: false),
                DestinationPostalCode = table.Column<string>(maxLength: 20, nullable: false),
                Status = table.Column<string>(maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                CreatedAtUnixMilliseconds = table.Column<long>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false),
                Version = table.Column<long>(nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_fulfillment_requests", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Type = table.Column<string>(maxLength: 200, nullable: false),
                Payload = table.Column<string>(nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(nullable: false),
                OccurredAtUnixMilliseconds = table.Column<long>(nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(nullable: true),
                Attempts = table.Column<int>(nullable: false),
                LastError = table.Column<string>(maxLength: 1_000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_fulfillment_requests_IdempotencyKey",
            table: "fulfillment_requests",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_fulfillment_requests_Status_CreatedAtUnixMilliseconds",
            table: "fulfillment_requests",
            columns: new[] { "Status", "CreatedAtUnixMilliseconds" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_PublishedAt_OccurredAtUnixMilliseconds",
            table: "outbox_messages",
            columns: new[] { "PublishedAt", "OccurredAtUnixMilliseconds" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fulfillment_requests");
        migrationBuilder.DropTable(name: "outbox_messages");
    }
}
