using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addClientCreditBlockOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreditBlockOverrideBy",
                schema: "dbo",
                table: "Client_TB",
                type: "nvarchar(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditBlockOverrideReason",
                schema: "dbo",
                table: "Client_TB",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CreditBlockOverrideUntil",
                schema: "dbo",
                table: "Client_TB",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditBlockOverrideBy",
                schema: "dbo",
                table: "Client_TB");

            migrationBuilder.DropColumn(
                name: "CreditBlockOverrideReason",
                schema: "dbo",
                table: "Client_TB");

            migrationBuilder.DropColumn(
                name: "CreditBlockOverrideUntil",
                schema: "dbo",
                table: "Client_TB");
        }
    }
}
