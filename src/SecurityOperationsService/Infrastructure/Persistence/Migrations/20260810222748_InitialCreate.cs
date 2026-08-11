using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vision.SecurityOperationsService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "security_operations");

            migrationBuilder.CreateTable(
                name: "hospitals",
                schema: "security_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hospitals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "security_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hospital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_buildings", x => x.id);
                    table.ForeignKey(
                        name: "fk_buildings_hospitals_hospital_id",
                        column: x => x.hospital_id,
                        principalSchema: "security_operations",
                        principalTable: "hospitals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                schema: "security_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    floor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                    table.ForeignKey(
                        name: "fk_locations_buildings_building_id",
                        column: x => x.building_id,
                        principalSchema: "security_operations",
                        principalTable: "buildings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_assets",
                schema: "security_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    asset_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    asset_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_service_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_assets_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "security_operations",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_incidents",
                schema: "security_operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_incidents", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_incidents_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "security_operations",
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_security_incidents_security_assets_security_asset_id",
                        column: x => x.security_asset_id,
                        principalSchema: "security_operations",
                        principalTable: "security_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buildings_hospital_id",
                schema: "security_operations",
                table: "buildings",
                column: "hospital_id");

            migrationBuilder.CreateIndex(
                name: "ix_buildings_hospital_id_name",
                schema: "security_operations",
                table: "buildings",
                columns: new[] { "hospital_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locations_building_id",
                schema: "security_operations",
                table: "locations",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "ix_locations_building_id_name",
                schema: "security_operations",
                table: "locations",
                columns: new[] { "building_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_security_assets_asset_tag",
                schema: "security_operations",
                table: "security_assets",
                column: "asset_tag",
                unique: true,
                filter: "asset_tag IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_security_assets_asset_type",
                schema: "security_operations",
                table: "security_assets",
                column: "asset_type");

            migrationBuilder.CreateIndex(
                name: "ix_security_assets_location_id",
                schema: "security_operations",
                table: "security_assets",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_assets_status",
                schema: "security_operations",
                table: "security_assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_security_assets_status_asset_type",
                schema: "security_operations",
                table: "security_assets",
                columns: new[] { "status", "asset_type" });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_created_at",
                schema: "security_operations",
                table: "security_incidents",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_location_id",
                schema: "security_operations",
                table: "security_incidents",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_security_asset_id",
                schema: "security_operations",
                table: "security_incidents",
                column: "security_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_severity",
                schema: "security_operations",
                table: "security_incidents",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_status",
                schema: "security_operations",
                table: "security_incidents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_status_severity",
                schema: "security_operations",
                table: "security_incidents",
                columns: new[] { "status", "severity" });

            migrationBuilder.CreateIndex(
                name: "ix_security_incidents_work_order_id",
                schema: "security_operations",
                table: "security_incidents",
                column: "work_order_id",
                unique: true,
                filter: "work_order_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_incidents",
                schema: "security_operations");

            migrationBuilder.DropTable(
                name: "security_assets",
                schema: "security_operations");

            migrationBuilder.DropTable(
                name: "locations",
                schema: "security_operations");

            migrationBuilder.DropTable(
                name: "buildings",
                schema: "security_operations");

            migrationBuilder.DropTable(
                name: "hospitals",
                schema: "security_operations");
        }
    }
}
