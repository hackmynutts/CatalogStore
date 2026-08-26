using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class newTableEventlog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Eventlog_TB",
                schema: "dbo",
                columns: table => new
                {
                    EventlogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TypeLog = table.Column<int>(type: "int", nullable: false),
                    EventDesc = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eventlog_TB", x => x.EventlogId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Eventlogs_CreatedOn",
                schema: "dbo",
                table: "Eventlog_TB",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Eventlogs_TableName_RecordID",
                schema: "dbo",
                table: "Eventlog_TB",
                columns: new[] { "TableName", "RecordID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Eventlog_TB",
                schema: "dbo");
        }
    }
}
