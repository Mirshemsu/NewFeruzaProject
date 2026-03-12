using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class addbanktransactionid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankTransferTransactionId",
                table: "DailyClosings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashBankTransactionId",
                table: "DailyClosings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalBankedAmount",
                table: "DailyClosings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankTransferTransactionId",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "CashBankTransactionId",
                table: "DailyClosings");

            migrationBuilder.DropColumn(
                name: "TotalBankedAmount",
                table: "DailyClosings");
        }
    }
}
