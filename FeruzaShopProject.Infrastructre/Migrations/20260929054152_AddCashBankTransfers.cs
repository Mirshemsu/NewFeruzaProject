using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddCashBankTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_BranchId",
                table: "DailyClosings");

            migrationBuilder.CreateTable(
                name: "CashBankTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBankTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBankTransfers_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashBankTransfers_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                UPDATE DailyClosings
                SET ClosingDate = CAST(ClosingDate AS date);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_OneActivePerBranchDate",
                table: "DailyClosings",
                columns: new[] { "BranchId", "ClosingDate" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CashBankTransfers_BranchId_TransferDate",
                table: "CashBankTransfers",
                columns: new[] { "BranchId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashBankTransfers_CreatedByUserId",
                table: "CashBankTransfers",
                column: "CreatedByUserId");

            // Days whose closing cash and bank differ from sales by the same
            // amount, with the total unchanged, are historical transfers.
            migrationBuilder.Sql("""
                INSERT INTO CashBankTransfers
                    (Id, BranchId, TransferDate, Direction, Amount, BankReference, Remarks, CreatedByUserId, IsActive, CreatedAt)
                SELECT
                    NEWID(),
                    c.BranchId,
                    CAST(c.ClosingDate AS date),
                    CASE WHEN c.TotalCashAmount > s.LiveCash THEN 1 ELSE 0 END,
                    ABS(c.TotalCashAmount - s.LiveCash),
                    NULL,
                    N'Imported from closing balance before transfer history existed',
                    c.ClosedBy,
                    1,
                    SYSUTCDATETIME()
                FROM DailyClosings c
                INNER JOIN (
                    SELECT
                        BranchId,
                        CAST(SaleDate AS date) AS SaleDate,
                        SUM(CASE WHEN PaymentMethod = 'Cash' THEN TotalAmount ELSE 0 END) AS LiveCash,
                        SUM(CASE WHEN PaymentMethod = 'Bank' THEN TotalAmount ELSE 0 END) AS LiveBank,
                        SUM(TotalAmount) AS LiveTotal
                    FROM DailySales
                    WHERE IsActive = 1
                    GROUP BY BranchId, CAST(SaleDate AS date)
                ) s ON s.BranchId = c.BranchId
                   AND s.SaleDate = CAST(c.ClosingDate AS date)
                WHERE c.IsActive = 1
                  AND c.TotalSalesAmount = s.LiveTotal
                  AND (c.TotalCashAmount - s.LiveCash) = (s.LiveBank - c.TotalBankAmount)
                  AND (c.TotalCashAmount - s.LiveCash) <> 0
                  AND EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id = c.ClosedBy);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashBankTransfers");

            migrationBuilder.DropIndex(
                name: "IX_DailyClosings_OneActivePerBranchDate",
                table: "DailyClosings");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosings_BranchId",
                table: "DailyClosings",
                column: "BranchId");
        }
    }
}
