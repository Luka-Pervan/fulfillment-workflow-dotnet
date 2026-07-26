using Fulfillment.Domain;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fulfillment.Infrastructure.Migrations;

[DbContext(typeof(FulfillmentDbContext))]
[Migration("202607260002_AddRequestHash")]
public partial class AddRequestHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestHash",
            table: "fulfillment_requests",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: FulfillmentRequest.LegacyRequestHash);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RequestHash",
            table: "fulfillment_requests");
    }
}
