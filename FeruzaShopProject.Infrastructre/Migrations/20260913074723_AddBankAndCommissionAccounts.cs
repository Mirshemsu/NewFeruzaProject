using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAndCommissionAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "DailySales",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "CreditPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AccountOwner = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionAccounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommissionLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommissionAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EntryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_CommissionAccounts_CommissionAccountId",
                        column: x => x.CommissionAccountId,
                        principalTable: "CommissionAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionLedgerEntries_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BankAccountId",
                table: "Transactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DailySales_BankAccountId",
                table: "DailySales",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPayments_BankAccountId",
                table: "CreditPayments",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_BranchId_AccountNumber",
                table: "BankAccounts",
                columns: new[] { "BranchId", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionAccounts_BranchId",
                table: "CommissionAccounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_CommissionAccountId",
                table: "CommissionLedgerEntries",
                column: "CommissionAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_EntryDate",
                table: "CommissionLedgerEntries",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionLedgerEntries_TransactionId",
                table: "CommissionLedgerEntries",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditPayments_BankAccounts_BankAccountId",
                table: "CreditPayments",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DailySales_BankAccounts_BankAccountId",
                table: "DailySales",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_BankAccounts_BankAccountId",
                table: "Transactions",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditPayments_BankAccounts_BankAccountId",
                table: "CreditPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_DailySales_BankAccounts_BankAccountId",
                table: "DailySales");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_BankAccounts_BankAccountId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "CommissionLedgerEntries");

            migrationBuilder.DropTable(
                name: "CommissionAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BankAccountId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_DailySales_BankAccountId",
                table: "DailySales");

            migrationBuilder.DropIndex(
                name: "IX_CreditPayments_BankAccountId",
                table: "CreditPayments");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "DailySales");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "CreditPayments");
        }
    }
}
