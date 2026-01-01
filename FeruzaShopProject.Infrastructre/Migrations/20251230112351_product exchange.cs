using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class productexchange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NewQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NewPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductExchanges_Products_NewProductId",
                        column: x => x.NewProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductExchanges_Products_OriginalProductId",
                        column: x => x.OriginalProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductExchanges_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductExchanges_Products_ProductId1",
                        column: x => x.ProductId1,
                        principalTable: "Products",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductExchanges_Transactions_OriginalTransactionId",
                        column: x => x.OriginalTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_CreatedAt",
                table: "ProductExchanges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_NewProductId",
                table: "ProductExchanges",
                column: "NewProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_OriginalProductId",
                table: "ProductExchanges",
                column: "OriginalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_OriginalTransactionId",
                table: "ProductExchanges",
                column: "OriginalTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_ProductId",
                table: "ProductExchanges",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_ProductId1",
                table: "ProductExchanges",
                column: "ProductId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductExchanges");
        }
    }
}
