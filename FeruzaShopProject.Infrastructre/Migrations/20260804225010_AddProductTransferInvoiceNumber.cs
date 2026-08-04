using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTransferInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "ProductTransfers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Existing rows: use TransferNumber so unique index can be created.
            migrationBuilder.Sql(@"
UPDATE ProductTransfers
SET InvoiceNumber = TransferNumber
WHERE InvoiceNumber IS NULL OR LTRIM(RTRIM(InvoiceNumber)) = '';
");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "ProductTransfers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTransfers_InvoiceNumber",
                table: "ProductTransfers",
                column: "InvoiceNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductTransfers_InvoiceNumber",
                table: "ProductTransfers");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "ProductTransfers");
        }
    }
}
