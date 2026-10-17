using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addSalesHeaderandSalesLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "SaleNumberSeq",
                schema: "dbo");

            migrationBuilder.CreateTable(
                name: "SalesHeader_TB",
                schema: "dbo",
                columns: table => new
                {
                    SalesHeaderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaleNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderID = table.Column<int>(type: "int", nullable: false),
                    ClientID = table.Column<int>(type: "int", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ClientIdentification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientCC = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ClientEmail = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ClientAddress = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    Seller = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApplyIva = table.Column<bool>(type: "bit", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IvaTotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentTerm = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false, computedColumnSql: "[TotalAmount] - [AmountPaid]", stored: true),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesHeader_TB", x => x.SalesHeaderID);
                    table.CheckConstraint("CK_SalesHeader_AmountPaid_NonNegative", "[AmountPaid] >= 0");
                    table.CheckConstraint("CK_SalesHeader_AmountPaid_NotAboveTotal", "[AmountPaid] <= [TotalAmount]");
                    table.ForeignKey(
                        name: "FK_SalesHeader_Client_ClientID",
                        column: x => x.ClientID,
                        principalSchema: "dbo",
                        principalTable: "Client_TB",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesHeader_Order_OrderID",
                        column: x => x.OrderID,
                        principalSchema: "dbo",
                        principalTable: "Order_TB",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesLine_TB",
                schema: "dbo",
                columns: table => new
                {
                    SalesLineID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    UnitPriceIVA = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LineSubtotal = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    LineIva = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    LineTotalPrice = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesLine_TB", x => x.SalesLineID);
                    table.CheckConstraint("CK_SalesLine_Discount_Valid", "[Discount] >= 0 AND [Discount] <= 99.99");
                    table.CheckConstraint("CK_SalesLine_LineTotalPrice_NonNegative", "[LineTotalPrice] >= 0");
                    table.CheckConstraint("CK_SalesLine_Quantity_Positive", "[Quantity] > 0");
                    table.CheckConstraint("CK_SalesLine_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_SalesLine_UnitPriceIVA_NonNegative", "[UnitPriceIVA] >= 0");
                    table.ForeignKey(
                        name: "FK_SalesLine_Product_ProductID",
                        column: x => x.ProductID,
                        principalSchema: "dbo",
                        principalTable: "Products_TB",
                        principalColumn: "ProductID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesLine_SalesHeader_SalesHeaderID",
                        column: x => x.SalesHeaderID,
                        principalSchema: "dbo",
                        principalTable: "SalesHeader_TB",
                        principalColumn: "SalesHeaderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_ClientID_SaleDate",
                schema: "dbo",
                table: "SalesHeader_TB",
                columns: new[] { "ClientID", "SaleDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_DueDate",
                schema: "dbo",
                table: "SalesHeader_TB",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_OrderID",
                schema: "dbo",
                table: "SalesHeader_TB",
                column: "OrderID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_PaymentStatus_DueDate",
                schema: "dbo",
                table: "SalesHeader_TB",
                columns: new[] { "PaymentStatus", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesHeader_SaleNumber",
                schema: "dbo",
                table: "SalesHeader_TB",
                column: "SaleNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLine_SalesHeaderID_ProductID",
                schema: "dbo",
                table: "SalesLine_TB",
                columns: new[] { "SalesHeaderID", "ProductID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesLine_TB_ProductID",
                schema: "dbo",
                table: "SalesLine_TB",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesLine_TB",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SalesHeader_TB",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SaleNumberSeq",
                schema: "dbo");
        }
    }
}
