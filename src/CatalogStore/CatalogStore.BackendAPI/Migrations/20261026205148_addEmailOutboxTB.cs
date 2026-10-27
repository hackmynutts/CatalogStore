using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addEmailOutboxTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailOutbox_TB",
                schema: "dbo",
                columns: table => new
                {
                    EmailOutboxID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ToName = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedTable = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RelatedID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox_TB", x => x.EmailOutboxID);
                    table.CheckConstraint("CK_EmailOutbox_Attempts_NonNegative", "[Attempts] >= 0");
                    table.CheckConstraint("CK_EmailOutbox_SentOn_Consistent", "([Status] = 1 AND [SentOn] IS NOT NULL) OR ([Status] <> 1 AND [SentOn] IS NULL)");
                    table.CheckConstraint("CK_EmailOutbox_Status_Valid", "[Status] IN (0,1,2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_RelatedTable_RelatedID",
                schema: "dbo",
                table: "EmailOutbox_TB",
                columns: new[] { "RelatedTable", "RelatedID" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_Status_NextAttemptOn",
                schema: "dbo",
                table: "EmailOutbox_TB",
                columns: new[] { "Status", "NextAttemptOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutbox_TB",
                schema: "dbo");
        }
    }
}
