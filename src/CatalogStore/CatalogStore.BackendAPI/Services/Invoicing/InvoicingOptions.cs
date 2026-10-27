namespace CatalogStore.BackendAPI.Services.Invoicing
{
    // Sección "Invoicing" de la configuración: los datos del emisor autorizados por el proveedor de facturación.
    // Con Enabled = false (por defecto) nada se envía: el botón Facturar solo permite ver la vista previa.
    // No hay ambiente de pruebas en el proveedor: Enabled = true emite comprobantes REALES ante Hacienda.
    public class InvoicingOptions
    {
        public bool Enabled { get; set; }
        // https://www.promosjym.com/gm/ApiJsonFacturacion.php (o el dominio que indique el proveedor)
        public string Endpoint { get; set; } = string.Empty;
        // encabezado.clientid: ID del perfil emisor. El 0 es un perfil válido, por eso es nullable y no "0 = sin configurar".
        public int? ProfileId { get; set; }
        // encabezado.cedulaemisor
        public string IssuerId { get; set; } = string.Empty;
        // encabezado.codigoactividad (ej. "4541.0")
        public string ActivityCode { get; set; } = string.Empty;
        // Con ceros a la izquierda: "001" y "00001". Son texto, nunca números.
        public string BranchCode { get; set; } = "001";
        public string TerminalCode { get; set; } = "00001";
        // encabezado.codigousuario y codigousuario de las consultas: usuario de registro de la integración.
        public string RegisterUser { get; set; } = string.Empty;
        // "1" = operación normal. Otros valores no activan un flujo de contingencia en el proveedor.
        public string SituationCode { get; set; } = "1";
        // Un tiempo agotado deja el comprobante "por conciliar": no se reenvía solo.
        public int TimeoutSeconds { get; set; } = 90;
        // Código de unidad del catálogo del proveedor para cada unidad de medida del sistema (nombre del enum → código).
        public Dictionary<string, string> UnitCodes { get; set; } = new()
        {
            ["Pieza"] = "Unid",
            ["Unidad"] = "Unid",
            ["Litro"] = "L",
            ["Caja"] = "Cj",
            ["Paquete"] = "Paq"
        };

        // Lo que falta para poder emitir; vacío = configuración completa.
        public List<string> MissingSettings()
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(Endpoint)) missing.Add("Endpoint");
            if (ProfileId is null or < 0) missing.Add("ProfileId");
            if (string.IsNullOrWhiteSpace(IssuerId)) missing.Add("IssuerId");
            if (string.IsNullOrWhiteSpace(ActivityCode)) missing.Add("ActivityCode");
            if (BranchCode.Length != 3 || !BranchCode.All(char.IsDigit)) missing.Add("BranchCode (3 dígitos)");
            if (TerminalCode.Length != 5 || !TerminalCode.All(char.IsDigit)) missing.Add("TerminalCode (5 dígitos)");
            if (string.IsNullOrWhiteSpace(RegisterUser)) missing.Add("RegisterUser");
            return missing;
        }
    }
}
