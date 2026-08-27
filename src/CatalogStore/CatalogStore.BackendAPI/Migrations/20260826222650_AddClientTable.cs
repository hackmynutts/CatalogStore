using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Client_TB",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Identification = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ClientPhone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClientEmail = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ClientAddress = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    StatusID = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Client_TB", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Client_Status_StatusID",
                        column: x => x.StatusID,
                        principalSchema: "dbo",
                        principalTable: "Status_TB",
                        principalColumn: "StatusID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Client_Identification_UQ",
                schema: "dbo",
                table: "Client_TB",
                column: "Identification",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Client_TB_StatusID",
                schema: "dbo",
                table: "Client_TB",
                column: "StatusID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Client_TB",
                schema: "dbo");
        }
    }
}
