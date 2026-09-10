using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addUnitMeasureProductTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnidadMedida",
                schema: "dbo",
                table: "Products_TB",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnidadMedida",
                schema: "dbo",
                table: "Products_TB");
        }
    }
}
