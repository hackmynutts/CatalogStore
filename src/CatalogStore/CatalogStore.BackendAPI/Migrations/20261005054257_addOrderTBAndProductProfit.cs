using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addOrderTBAndProductProfit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "OrderNumberSeq",
                schema: "dbo");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitPercentage",
                schema: "dbo",
                table: "Products_TB",
                type: "decimal(6,4)",
                precision: 6,
                scale: 4,
                nullable: false,
                defaultValue: 1.07m);

            migrationBuilder.CreateTable(
                name: "Order_TB",
                schema: "dbo",
                columns: table => new
                {
                    OrderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientID = table.Column<int>(type: "int", nullable: false),
                    OrderTotalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order_TB", x => x.OrderID);
                    table.CheckConstraint("CK_Order_TotalAmount_NonNegative", "[OrderTotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_Order_Client_ClientID",
                        column: x => x.ClientID,
                        principalSchema: "dbo",
                        principalTable: "Client_TB",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLine_TB",
                schema: "dbo",
                columns: table => new
                {
                    OrderLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UnitPriceIVA = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LineTotalPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 0.0m),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLine_TB", x => x.OrderLineID);
                    table.CheckConstraint("CK_OrderLine_Discount_Valid", "[Discount] >= 0 AND [Discount] <= 99.99");
                    table.CheckConstraint("CK_OrderLine_LineTotalPrice_NonNegative", "[LineTotalPrice] >= 0");
                    table.CheckConstraint("CK_OrderLine_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_OrderLine_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_OrderLine_UnitPriceIVA_NonNegative", "[UnitPriceIVA] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderLine_Order_OrderID",
                        column: x => x.OrderID,
                        principalSchema: "dbo",
                        principalTable: "Order_TB",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderLine_Product_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products_TB",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_ProfitPercentage_Valid",
                schema: "dbo",
                table: "Products_TB",
                sql: "[ProfitPercentage] >= 1 AND [ProfitPercentage] <= 10");

            migrationBuilder.CreateIndex(
                name: "IX_Order_ClientID_CreatedOn",
                schema: "dbo",
                table: "Order_TB",
                columns: new[] { "ClientID", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Order_OrderNumber",
                schema: "dbo",
                table: "Order_TB",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Order_OrderStatus",
                schema: "dbo",
                table: "Order_TB",
                column: "OrderStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_OrderID_ProductID",
                schema: "dbo",
                table: "OrderLine_TB",
                columns: new[] { "OrderID", "ProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLine_TB_ProductID",
                schema: "dbo",
                table: "OrderLine_TB",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLine_TB",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Order_TB",
                schema: "dbo");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_ProfitPercentage_Valid",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "ProfitPercentage",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropSequence(
                name: "OrderNumberSeq",
                schema: "dbo");
        }
    }
}
