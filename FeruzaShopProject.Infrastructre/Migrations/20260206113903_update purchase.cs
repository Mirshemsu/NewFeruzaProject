using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class updatepurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuantityReceived",
                table: "PurchaseOrderItems",
                newName: "QuantityRegistered");

            migrationBuilder.RenameColumn(
                name: "QuantityOrdered",
                table: "PurchaseOrderItems",
                newName: "QuantityRequested");

            migrationBuilder.RenameColumn(
                name: "QuantityApproved",
                table: "PurchaseOrderItems",
                newName: "QuantityAccepted");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<bool>(
                name: "FinanceVerified",
                table: "PurchaseOrderItems",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinanceVerified",
                table: "PurchaseOrderItems");

            migrationBuilder.RenameColumn(
                name: "QuantityRequested",
                table: "PurchaseOrderItems",
                newName: "QuantityOrdered");

            migrationBuilder.RenameColumn(
                name: "QuantityRegistered",
                table: "PurchaseOrderItems",
                newName: "QuantityReceived");

            migrationBuilder.RenameColumn(
                name: "QuantityAccepted",
                table: "PurchaseOrderItems",
                newName: "QuantityApproved");

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
