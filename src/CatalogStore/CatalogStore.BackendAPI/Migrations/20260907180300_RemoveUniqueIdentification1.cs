using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueIdentification1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Client_Identification_UQ",
                schema: "dbo",
                table: "Client_TB");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Client_Identification_UQ",
                schema: "dbo",
                table: "Client_TB",
                column: "Identification",
                unique: true);
        }
    }
}
