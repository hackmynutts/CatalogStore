namespace CatalogStore.UI.Documents
{
    // Datos de la empresa que emite la factura (SAM DESIGN), que van en el encabezado de la factura y la nota de crédito.
    // Se leen de la sección "Issuer" de la configuración, así se pueden cambiar sin publicar código.
    public static class InvoiceIssuer
    {
        public static string LegalName(IConfiguration config) => (config["Issuer:LegalName"] ?? string.Empty).Trim();

        // Las líneas de detalle, en orden: dirección, ubicación, correo, teléfono y cédula jurídica. Las vacías se omiten.
        public static IReadOnlyList<string> Lines(IConfiguration config)
        {
            string Get(string key) => (config[key] ?? string.Empty).Trim();
            var lines = new List<string>();
            void Add(string text) { if (text.Length > 0) lines.Add(text); }

            Add(Get("Issuer:Address"));
            Add(Get("Issuer:Location"));
            var email = Get("Issuer:Email");
            if (email.Length == 0) email = Get("Landing:Email");
            if (email.Length > 0) Add("Correo: " + email);
            var phone = Get("Issuer:Phone");
            if (phone.Length > 0) Add("Teléfono: " + phone);
            var id = Get("Issuer:Identification");
            if (id.Length > 0) Add("No. Ident: " + id);
            return lines;
        }
    }
}
