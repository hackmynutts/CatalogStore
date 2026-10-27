using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addInvoiceTB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoice_TB",
                schema: "dbo",
                columns: table => new
                {
                    InvoiceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SalesHeaderID = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    ReferenceInvoiceID = table.Column<int>(type: "int", nullable: true),
                    Clave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Consecutive = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SecurityCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    XmlBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HaciendaStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HaciendaMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PdfUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PdfUrlTicket = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SentOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoice_TB", x => x.InvoiceID);
                    table.CheckConstraint("CK_Invoice_Clave_Format", "[Clave] IS NULL OR (LEN([Clave]) = 50 AND [Clave] NOT LIKE '%[^0-9]%')");
                    table.CheckConstraint("CK_Invoice_DocType_Valid", "[DocumentType] IN (1,3,4)");
                    table.CheckConstraint("CK_Invoice_Reference_Consistent", "([DocumentType] = 3 AND [ReferenceInvoiceID] IS NOT NULL) OR ([DocumentType] <> 3 AND [ReferenceInvoiceID] IS NULL)");
                    table.CheckConstraint("CK_Invoice_Status_Valid", "[Status] IN (0,1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_Invoice_Invoice_ReferenceInvoiceID",
                        column: x => x.ReferenceInvoiceID,
                        principalSchema: "dbo",
                        principalTable: "Invoice_TB",
                        principalColumn: "InvoiceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoice_SalesHeader_SalesHeaderID",
                        column: x => x.SalesHeaderID,
                        principalSchema: "dbo",
                        principalTable: "SalesHeader_TB",
                        principalColumn: "SalesHeaderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_SalesHeaderID",
                schema: "dbo",
                table: "Invoice_TB",
                column: "SalesHeaderID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoice_TB_ReferenceInvoiceID",
                schema: "dbo",
                table: "Invoice_TB",
                column: "ReferenceInvoiceID");

            migrationBuilder.CreateIndex(
                name: "UX_Invoice_Clave",
                schema: "dbo",
                table: "Invoice_TB",
                column: "Clave",
                unique: true,
                filter: "[Clave] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Invoice_Sale_DocType_Active",
                schema: "dbo",
                table: "Invoice_TB",
                columns: new[] { "SalesHeaderID", "DocumentType" },
                unique: true,
                filter: "[Status] IN (0,1,2,4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoice_TB",
                schema: "dbo");
        }
    }
}
