using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vision.WorkOrderService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "work_orders");

            migrationBuilder.CreateTable(
                name: "technicians",
                schema: "work_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    cognito_subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    specialty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technicians", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "work_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_technician_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completion_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    asset_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    location_name_snapshot = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_orders_technicians_assigned_technician_id",
                        column: x => x.assigned_technician_id,
                        principalSchema: "work_orders",
                        principalTable: "technicians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "technician_notes",
                schema: "work_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_technician_notes_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "work_orders",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_technician_notes_work_order_id",
                schema: "work_orders",
                table: "technician_notes",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_technicians_email",
                schema: "work_orders",
                table: "technicians",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technicians_is_active",
                schema: "work_orders",
                table: "technicians",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_assigned_technician_id",
                schema: "work_orders",
                table: "work_orders",
                column: "assigned_technician_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_security_asset_id",
                schema: "work_orders",
                table: "work_orders",
                column: "security_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_security_incident_id",
                schema: "work_orders",
                table: "work_orders",
                column: "security_incident_id",
                unique: true,
                filter: "security_incident_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_source_event_id",
                schema: "work_orders",
                table: "work_orders",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_status",
                schema: "work_orders",
                table: "work_orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "technician_notes",
                schema: "work_orders");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "work_orders");

            migrationBuilder.DropTable(
                name: "technicians",
                schema: "work_orders");
        }
    }
}
