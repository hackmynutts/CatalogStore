using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addInventoryTransactionTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryTransaction_TB",
                schema: "dbo",
                columns: table => new
                {
                    InventoryTransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryLineID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TransactionQuantity = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    OnHoldBefore = table.Column<int>(type: "int", nullable: false),
                    OnHoldAfter = table.Column<int>(type: "int", nullable: false),
                    ReferenceReason = table.Column<int>(type: "int", nullable: false),
                    ReferenceID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransaction_TB", x => x.InventoryTransactionID);
                    table.CheckConstraint("CK_InventoryTransaction_OnHoldAfter_NonNegative", "[OnHoldAfter] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_OnHoldBefore_NonNegative", "[OnHoldBefore] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_QuantityAfter_NonNegative", "[QuantityAfter] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_QuantityBefore_NonNegative", "[QuantityBefore] >= 0");
                    table.CheckConstraint("CK_InventoryTransaction_TransactionQuantity_Positive", "[TransactionQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryTransaction_InventoryLine_InventoryLineID",
                        column: x => x.InventoryLineID,
                        principalSchema: "dbo",
                        principalTable: "InventoryLine_TB",
                        principalColumn: "InventoryLineID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_InventoryLineID_CreatedOn",
                schema: "dbo",
                table: "InventoryTransaction_TB",
                columns: new[] { "InventoryLineID", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransaction_ReferenceReason_ReferenceID",
                schema: "dbo",
                table: "InventoryTransaction_TB",
                columns: new[] { "ReferenceReason", "ReferenceID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryTransaction_TB",
                schema: "dbo");
        }
    }
}
