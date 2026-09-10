using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addDeliveryPartnerClientTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryPartner",
                schema: "dbo",
                table: "Client_TB",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryPartner",
                schema: "dbo",
                table: "Client_TB");
        }
    }
}
