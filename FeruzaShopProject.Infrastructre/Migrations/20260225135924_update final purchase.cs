using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class updatefinalpurchase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedBy",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

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

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRegistrationEditAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
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

            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredBy",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationEditCount",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderItemId",
                table: "PurchaseHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseHistory_PurchaseOrderItemId",
                table: "PurchaseHistory",
                column: "PurchaseOrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory",
                column: "PurchaseOrderItemId",
                principalTable: "PurchaseOrderItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseHistory_PurchaseOrderItems_PurchaseOrderItemId",
                table: "PurchaseHistory");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseHistory_PurchaseOrderItemId",
                table: "PurchaseHistory");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "AcceptedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "FinanceVerifiedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LastRegistrationEditAt",
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
                name: "RegisteredAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "RegisteredBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "RegistrationEditCount",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderItemId",
                table: "PurchaseHistory");
        }
    }
}
