using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogStore.BackendAPI.Migrations
{
    /// <inheritdoc />
    public partial class addInvoicingFieldsClientProductSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientIdentificationType",
                schema: "dbo",
                table: "SalesHeader_TB",
                type: "int",
                nullable: false,
                defaultValue: 1); // Cambiado a mano de 0 a 1: 0 no es un tipo válido; 1 = cédula física

            migrationBuilder.AddColumn<string>(
                name: "ClientPhone",
                schema: "dbo",
                table: "SalesHeader_TB",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CabysCode",
                schema: "dbo",
                table: "Products_TB",
                type: "nvarchar(13)",
                maxLength: 13,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentificationType",
                schema: "dbo",
                table: "Client_TB",
                type: "int",
                nullable: false,
                defaultValue: 1); // Cambiado a mano de 0 a 1: 0 no es un tipo válido; 1 = cédula física

            // Agregado a mano: deduce el tipo de los clientes que ya existen a partir del formato de la cédula.
            // Jurídica: 10 dígitos que empiezan con 3. DIMEX: 11 o 12 dígitos. El resto queda como física.
            // Es una aproximación: confirmar con la búsqueda de Hacienda al editar cada cliente.
            migrationBuilder.Sql(@"
UPDATE dbo.Client_TB SET IdentificationType = 2 WHERE LEN(Identification) = 10 AND Identification LIKE '3%';
UPDATE dbo.Client_TB SET IdentificationType = 3 WHERE LEN(Identification) IN (11, 12);");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_CabysCode_Format",
                schema: "dbo",
                table: "Products_TB",
                sql: "[CabysCode] IS NULL OR (LEN([CabysCode]) = 13 AND [CabysCode] NOT LIKE '%[^0-9]%')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_CabysCode_Format",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "ClientIdentificationType",
                schema: "dbo",
                table: "SalesHeader_TB");

            migrationBuilder.DropColumn(
                name: "ClientPhone",
                schema: "dbo",
                table: "SalesHeader_TB");

            migrationBuilder.DropColumn(
                name: "CabysCode",
                schema: "dbo",
                table: "Products_TB");

            migrationBuilder.DropColumn(
                name: "IdentificationType",
                schema: "dbo",
                table: "Client_TB");
        }
    }
}
