using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addOrderPendingSince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingSince",
                schema: "dbo",
                table: "Order_TB",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_PendingSince",
                schema: "dbo",
                table: "Order_TB",
                column: "PendingSince",
                filter: "[OrderStatus] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Order_PendingSince",
                schema: "dbo",
                table: "Order_TB");

            migrationBuilder.DropColumn(
                name: "PendingSince",
                schema: "dbo",
                table: "Order_TB");
        }
    }
}
