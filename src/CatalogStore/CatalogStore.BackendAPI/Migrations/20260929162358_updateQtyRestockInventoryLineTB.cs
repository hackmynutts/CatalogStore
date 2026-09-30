using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class updateQtyRestockInventoryLineTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuantityReorder",
                schema: "dbo",
                table: "InventoryLine_TB",
                newName: "QuantityRestock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuantityRestock",
                schema: "dbo",
                table: "InventoryLine_TB",
                newName: "QuantityReorder");
        }
    }
}
