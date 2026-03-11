using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class updatepurchaseflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "AcceptedBy",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "LastRegistrationEditAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "QuantityAccepted",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "QuantityRegistered",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "QuantityRequested",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "RegisteredAt",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "RegisteredBy",
                table: "PurchaseOrderItems");

            migrationBuilder.RenameColumn(
                name: "RegistrationEditCount",
                table: "PurchaseOrderItems",
                newName: "Quantity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "PurchaseOrderItems",
                newName: "RegistrationEditCount");

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
                name: "LastRegistrationEditAt",
                table: "PurchaseOrderItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityAccepted",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityRegistered",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityRequested",
                table: "PurchaseOrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

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
        }
    }
}
