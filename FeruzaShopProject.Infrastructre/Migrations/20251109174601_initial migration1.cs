using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeruzaShopProject.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class initialmigration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Branches_BranchId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Branches_BranchId1",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_BranchId1",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "BranchId1",
                table: "Stocks");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Branches_BranchId",
                table: "Stocks",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Branches_BranchId",
                table: "Stocks");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId1",
                table: "Stocks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_BranchId1",
                table: "Stocks",
                column: "BranchId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Branches_BranchId",
                table: "Stocks",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Branches_BranchId1",
                table: "Stocks",
                column: "BranchId1",
                principalTable: "Branches",
                principalColumn: "Id");
        }
    }
}
