using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addOneInProcessOrderPerClientIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Order_ClientID_InProcess_UQ",
                schema: "dbo",
                table: "Order_TB",
                column: "ClientID",
                unique: true,
                filter: "[OrderStatus] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Order_ClientID_InProcess_UQ",
                schema: "dbo",
                table: "Order_TB");
        }
    }
}
