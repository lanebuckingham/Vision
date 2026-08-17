using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                schema: "security_operations",
                table: "outbox_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trace_state",
                schema: "security_operations",
                table: "outbox_messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trace_parent",
                schema: "security_operations",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "trace_state",
                schema: "security_operations",
                table: "outbox_messages");
        }
    }
}
