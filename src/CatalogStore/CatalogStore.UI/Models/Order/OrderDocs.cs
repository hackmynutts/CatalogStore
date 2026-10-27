namespace CatalogStore.UI.Models.Order
{
    // Documentos impresos que salen de una orden. La proforma no es fiscal.
    public static class OrderDocs
    {
        // Mismo valor que Proforma.ValidDays del backend (se usa en el correo); si uno cambia, cambia el otro.
        public const int ProformaValidDays = 8;

        // SAM0000123 → PRO-0000123: la proforma toma el número de su orden (igual que en el correo).
        public static string ProformaNumber(string orderNumber)
        {
            var digits = new string((orderNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            return "PRO-" + (digits.Length == 0 ? orderNumber : digits);
        }

        // Hoy en Costa Rica: el servidor puede estar en otra zona.
        public static DateTime LocalNow() => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Costa_Rica");
    }
}
