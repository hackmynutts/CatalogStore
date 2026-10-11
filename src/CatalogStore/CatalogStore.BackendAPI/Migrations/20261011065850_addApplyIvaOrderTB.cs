using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addApplyIvaOrderTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cambiado a mano de false a true: si hubiera órdenes existentes, deben seguir llevando IVA.
            migrationBuilder.AddColumn<bool>(
                name: "ApplyIva",
                schema: "dbo",
                table: "Order_TB",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplyIva",
                schema: "dbo",
                table: "Order_TB");
        }
    }
}
