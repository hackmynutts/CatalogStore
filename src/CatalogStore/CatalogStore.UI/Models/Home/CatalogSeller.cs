namespace CatalogStore.UI.Models.Home
{
    // Un vendedor al que el visitante del catálogo público puede escribirle por WhatsApp.
    // Salen de la configuración "Landing:Sellers" (Name y WhatsApp de cada uno); si no hay ninguno se usa el WhatsApp general de "Landing:WhatsApp".
    public record CatalogSeller(string Name, string WhatsApp)
    {
        public const int MaxNameLength = 60;

        // Solo dígitos para el enlace wa.me. Un número de 8 dígitos se toma como de Costa Rica (506).
        public static string NormalizeNumber(string? raw)
        {
            var digits = new string((raw ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 8) digits = "506" + digits;
            return digits.Length is >= 10 and <= 15 ? digits : string.Empty;
        }

        public static List<CatalogSeller> Load(IConfiguration configuration)
        {
            var list = new List<CatalogSeller>();
            foreach (var section in configuration.GetSection("Landing:Sellers").GetChildren())
            {
                var name = (section["Name"] ?? string.Empty).Trim();
                var number = NormalizeNumber(section["WhatsApp"]);
                if (name.Length == 0 || number.Length == 0) continue;
                if (name.Length > MaxNameLength) name = name[..MaxNameLength];
                if (list.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(new CatalogSeller(name, number));
            }
            return list;
        }

        // Opciones del selector: cada vendedor y, al final, la atención general (si está configurada).
        public static List<CatalogSeller> Options(IConfiguration configuration, string generalLabel = "Atención general")
        {
            var options = Load(configuration);
            var general = NormalizeNumber(configuration["Landing:WhatsApp"]);
            if (general.Length > 0 && options.All(o => o.WhatsApp != general)) options.Add(new CatalogSeller(generalLabel, general));
            return options;
        }
    }

    // Una línea de la lista de consulta: el producto y cuántas unidades quiere consultar.
    public record ConsultationLine(PublicCatalogItem Item, int Quantity);
}
