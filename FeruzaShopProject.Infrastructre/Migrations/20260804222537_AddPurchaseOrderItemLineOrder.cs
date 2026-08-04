using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderItemLineOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems");

            migrationBuilder.AddColumn<int>(
                name: "LineOrder",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Best-effort backfill for existing rows (CreatedAt, then Id).
            migrationBuilder.Sql(@"
;WITH Ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY PurchaseOrderId ORDER BY CreatedAt, Id) - 1 AS Rn
    FROM PurchaseOrderItems
)
UPDATE poi
SET LineOrder = r.Rn
FROM PurchaseOrderItems poi
INNER JOIN Ranked r ON poi.Id = r.Id;
");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId_LineOrder",
                table: "PurchaseOrderItems",
                columns: new[] { "PurchaseOrderId", "LineOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId_LineOrder",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LineOrder",
                table: "PurchaseOrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");
        }
    }
}
