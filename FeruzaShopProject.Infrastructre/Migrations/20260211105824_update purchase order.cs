using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class updatepurchaseorder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BuyingPrice",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyingPrice",
                table: "PurchaseOrderItems");
        }
    }
}
