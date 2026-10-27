using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addGoalTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Goal_TB",
                schema: "dbo",
                columns: table => new
                {
                    GoalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerUserName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    SalesAmountTarget = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CollectedAmountTarget = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SalesCountTarget = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goal_TB", x => x.GoalID);
                    table.CheckConstraint("CK_Goal_Month_Valid", "[Month] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_Goal_Targets_NonNegative", "[SalesAmountTarget] >= 0 AND [CollectedAmountTarget] >= 0 AND [SalesCountTarget] >= 0");
                    table.CheckConstraint("CK_Goal_Year_Valid", "[Year] BETWEEN 2024 AND 2200");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_Seller_SaleDate",
                schema: "dbo",
                table: "SalesHeader_TB",
                columns: new[] { "Seller", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Goal_Seller_Year_Month_UQ",
                schema: "dbo",
                table: "Goal_TB",
                columns: new[] { "SellerUserName", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Goal_TB",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_SalesHeader_Seller_SaleDate",
                schema: "dbo",
                table: "SalesHeader_TB");
        }
    }
}
