using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addSalesPaymentTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesPayment_TB",
                schema: "dbo",
                columns: table => new
                {
                    SalesPaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(75)", maxLength: 75, nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    VoidedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesPayment_TB", x => x.SalesPaymentID);
                    table.CheckConstraint("CK_SalesPayment_Amount_Positive", "[Amount] > 0");
                    table.CheckConstraint("CK_SalesPayment_Method_Valid", "[PaymentMethod] IN (1,2,3,4,6,7,99)");
                    table.CheckConstraint("CK_SalesPayment_Void_Consistent", "([IsVoided] = 0 AND [VoidedBy] IS NULL AND [VoidedOn] IS NULL AND [VoidedReason] IS NULL) OR ([IsVoided] = 1 AND [VoidedBy] IS NOT NULL AND [VoidedOn] IS NOT NULL AND [VoidedReason] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SalesPayment_SalesHeader_SalesHeaderID",
                        column: x => x.SalesHeaderID,
                        principalSchema: "dbo",
                        principalTable: "SalesHeader_TB",
                        principalColumn: "SalesHeaderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesPayment_SalesHeaderID_PaymentDate",
                schema: "dbo",
                table: "SalesPayment_TB",
                columns: new[] { "SalesHeaderID", "PaymentDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesPayment_TB",
                schema: "dbo");
        }
    }
}
