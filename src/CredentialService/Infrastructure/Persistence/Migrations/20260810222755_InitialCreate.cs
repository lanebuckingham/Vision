using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vision.CredentialService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "credentials");

            migrationBuilder.CreateTable(
                name: "people",
                schema: "credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    person_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    employee_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    job_title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_people", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "credentials",
                schema: "credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credentials", x => x.id);
                    table.ForeignKey(
                        name: "fk_credentials_people_person_id",
                        column: x => x.person_id,
                        principalSchema: "credentials",
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credentials_credential_number",
                schema: "credentials",
                table: "credentials",
                column: "credential_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_credentials_expires_at",
                schema: "credentials",
                table: "credentials",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_person_id",
                schema: "credentials",
                table: "credentials",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_credentials_revoked_at",
                schema: "credentials",
                table: "credentials",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_people_employee_number",
                schema: "credentials",
                table: "people",
                column: "employee_number",
                unique: true,
                filter: "employee_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_people_is_active",
                schema: "credentials",
                table: "people",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_people_last_name_first_name",
                schema: "credentials",
                table: "people",
                columns: new[] { "last_name", "first_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credentials",
                schema: "credentials");

            migrationBuilder.DropTable(
                name: "people",
                schema: "credentials");
        }
    }
}
