using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addVoidFieldsSalesHeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidedBy",
                schema: "dbo",
                table: "SalesHeader_TB",
                type: "nvarchar(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedOn",
                schema: "dbo",
                table: "SalesHeader_TB",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidedReason",
                schema: "dbo",
                table: "SalesHeader_TB",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesHeader_Void_Consistent",
                schema: "dbo",
                table: "SalesHeader_TB",
                sql: "([PaymentStatus] != 3 AND [VoidedBy] IS NULL AND [VoidedOn] IS NULL AND [VoidedReason] IS NULL) OR ([PaymentStatus] = 3 AND [VoidedBy] IS NOT NULL AND [VoidedOn] IS NOT NULL AND [VoidedReason] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesHeader_Void_Consistent",
                schema: "dbo",
                table: "SalesHeader_TB");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                schema: "dbo",
                table: "SalesHeader_TB");

            migrationBuilder.DropColumn(
                name: "VoidedOn",
                schema: "dbo",
                table: "SalesHeader_TB");

            migrationBuilder.DropColumn(
                name: "VoidedReason",
                schema: "dbo",
                table: "SalesHeader_TB");
        }
    }
}
