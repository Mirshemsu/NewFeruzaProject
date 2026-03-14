using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class updatepurchaseinvoicenumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductExchanges_Products_ProductId",
                table: "ProductExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductExchanges_Products_ProductId1",
                table: "ProductExchanges");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistory_AspNetUsers_PerformedByUserId",
                table: "PurchaseHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory");

            migrationBuilder.DropIndex(
                name: "IX_ProductExchanges_ProductId",
                table: "ProductExchanges");

            migrationBuilder.DropIndex(
                name: "IX_ProductExchanges_ProductId1",
                table: "ProductExchanges");

            migrationBuilder.DropIndex(
                name: "IX_DailySales_SaleDate_PaymentMethod",
                table: "DailySales");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FinanceVerified",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PriceEditCount",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PriceSetAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PriceSetBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "ProductExchanges");

            migrationBuilder.DropColumn(
                name: "ProductId1",
                table: "ProductExchanges");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "PurchaseOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceVerifiedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceVerifiedBy",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "PurchaseOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierName",
                table: "PurchaseOrderItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "PurchaseHistory",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "PurchaseHistory",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DailyClosings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedBy",
                table: "PurchaseOrders",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_FinanceVerifiedBy",
                table: "PurchaseOrders",
                column: "FinanceVerifiedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistory_AspNetUsers_PerformedByUserId",
                table: "PurchaseHistory",
                column: "PerformedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory",
                column: "PurchaseOrderItemId",
                principalTable: "PurchaseOrderItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_ApprovedBy",
                table: "PurchaseOrders",
                column: "ApprovedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_FinanceVerifiedBy",
                table: "PurchaseOrders",
                column: "FinanceVerifiedBy",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistory_AspNetUsers_PerformedByUserId",
                table: "PurchaseHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_ApprovedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_FinanceVerifiedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ApprovedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_FinanceVerifiedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "PurchaseOrders");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "SupplierName",
                table: "PurchaseOrderItems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinanceVerified",
                table: "PurchaseOrderItems",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinanceVerifiedAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceVerifiedBy",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceEditCount",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PriceSetAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceSetBy",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "PurchaseHistory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "PurchaseHistory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "ProductExchanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId1",
                table: "ProductExchanges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "DailyClosings",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_ProductId",
                table: "ProductExchanges",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductExchanges_ProductId1",
                table: "ProductExchanges",
                column: "ProductId1");

            migrationBuilder.CreateIndex(
                name: "IX_DailySales_SaleDate_PaymentMethod",
                table: "DailySales",
                columns: new[] { "SaleDate", "PaymentMethod" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProductExchanges_Products_ProductId",
                table: "ProductExchanges",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductExchanges_Products_ProductId1",
                table: "ProductExchanges",
                column: "ProductId1",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistory_AspNetUsers_PerformedByUserId",
                table: "PurchaseHistory",
                column: "PerformedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory",
                column: "PurchaseOrderItemId",
                principalTable: "PurchaseOrderItems",
                principalColumn: "Id");
        }
    }
}
