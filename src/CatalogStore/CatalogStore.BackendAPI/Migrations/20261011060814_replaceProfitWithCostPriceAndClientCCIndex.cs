using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class replaceProfitWithCostPriceAndClientCCIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_ProfitPercentage_Valid",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "ProfitPercentage",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                schema: "dbo",
                table: "Products_TB",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                schema: "dbo",
                table: "OrderLine_TB",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_CostPrice_NonNegative",
                schema: "dbo",
                table: "Products_TB",
                sql: "[CostPrice] IS NULL OR [CostPrice] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Client_CC",
                schema: "dbo",
                table: "Client_TB",
                column: "CC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_CostPrice_NonNegative",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropIndex(
                name: "IX_Client_CC",
                schema: "dbo",
                table: "Client_TB");

            migrationBuilder.DropColumn(
                name: "CostPrice",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                schema: "dbo",
                table: "OrderLine_TB");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitPercentage",
                schema: "dbo",
                table: "Products_TB",
                type: "decimal(6,4)",
                precision: 6,
                scale: 4,
                nullable: false,
                defaultValue: 1.07m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_ProfitPercentage_Valid",
                schema: "dbo",
                table: "Products_TB",
                sql: "[ProfitPercentage] >= 1 AND [ProfitPercentage] <= 10");
        }
    }
}
