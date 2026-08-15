using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vision.WorkOrderService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_work_orders_created_at",
                schema: "work_orders",
                table: "work_orders",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_priority",
                schema: "work_orders",
                table: "work_orders",
                column: "priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_orders_created_at",
                schema: "work_orders",
                table: "work_orders");

            migrationBuilder.DropIndex(
                name: "ix_work_orders_priority",
                schema: "work_orders",
                table: "work_orders");
        }
    }
}
