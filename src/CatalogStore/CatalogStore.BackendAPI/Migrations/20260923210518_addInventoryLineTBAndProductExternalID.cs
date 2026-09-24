using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addInventoryLineTBAndProductExternalID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalProductID",
                schema: "dbo",
                table: "Products_TB",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryLine_TB",
                schema: "dbo",
                columns: table => new
                {
                    InventoryLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    QuantityAvailable = table.Column<int>(type: "int", nullable: false, computedColumnSql: "[Quantity] - [QuantityOnHold]", stored: true),
                    QuantityReorder = table.Column<int>(type: "int", nullable: false),
                    QuantityOnHold = table.Column<int>(type: "int", nullable: false),
                    LastRestock = table.Column<DateOnly>(type: "date", nullable: true),
                    StatusID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLine_TB", x => x.InventoryLineID);
                    table.CheckConstraint("CK_InventoryLine_OnHold_LTE_Quantity", "[QuantityOnHold] <= [Quantity]");
                    table.CheckConstraint("CK_InventoryLine_Quantity_NonNegative", "[Quantity] >= 0");
                    table.CheckConstraint("CK_InventoryLine_QuantityOnHold_NonNegative", "[QuantityOnHold] >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryLine_Inventory_InventoryID",
                        column: x => x.InventoryID,
                        principalSchema: "dbo",
                        principalTable: "Inventory_TB",
                        principalColumn: "InventoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLine_Product_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products_TB",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLine_Status_StatusID",
                        column: x => x.StatusID,
                        principalSchema: "dbo",
                        principalTable: "Status_TB",
                        principalColumn: "StatusID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ExternalProductID_UQ",
                schema: "dbo",
                table: "Products_TB",
                column: "ExternalProductID",
                unique: true,
                filter: "[ExternalProductID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLine_InventoryID_ProductID",
                schema: "dbo",
                table: "InventoryLine_TB",
                columns: new[] { "InventoryID", "ProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLine_TB_ProductID",
                schema: "dbo",
                table: "InventoryLine_TB",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLine_TB_StatusID",
                schema: "dbo",
                table: "InventoryLine_TB",
                column: "StatusID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryLine_TB",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Products_ExternalProductID_UQ",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "ExternalProductID",
                schema: "dbo",
                table: "Products_TB");
        }
    }
}
