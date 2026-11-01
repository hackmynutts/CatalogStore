using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addSalesExchangeTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesExchange_TB",
                schema: "dbo",
                columns: table => new
                {
                    SalesExchangeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderID = table.Column<int>(type: "int", nullable: false),
                    SalesLineID = table.Column<int>(type: "int", nullable: false),
                    ProductID = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesExchange_TB", x => x.SalesExchangeID);
                    table.CheckConstraint("CK_SalesExchange_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_SalesExchange_SalesHeader_SalesHeaderID",
                        column: x => x.SalesHeaderID,
                        principalSchema: "dbo",
                        principalTable: "SalesHeader_TB",
                        principalColumn: "SalesHeaderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesExchange_SalesHeaderID_CreatedOn",
                schema: "dbo",
                table: "SalesExchange_TB",
                columns: new[] { "SalesHeaderID", "CreatedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesExchange_TB",
                schema: "dbo");
        }
    }
}
