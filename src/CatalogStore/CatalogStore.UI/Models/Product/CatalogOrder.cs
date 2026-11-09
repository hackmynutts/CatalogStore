using System.Globalization;
using System.Text.RegularExpressions;

namespace CatalogStore.UI.Models.Product
{
    // Orden de los catálogos: categoría → modelo → color → talla, para que las variantes de un mismo producto queden seguidas
    // (todos los rojos juntos, luego los rosados) y las tallas vayan en su orden natural (S, M, L, XL) y no alfabético.
    //
    // Los nombres siguen el formato "MODELO | COLOR TALLA" (por ejemplo "GUANTE BIKE LIFE | ROJO M"), pero también aparecen con la talla
    // pegada al modelo ("GUANTE BIKE LIFE S | AZUL") o sin barra ("CAUCHO CAMPANA MONTA CARGA XRE 300"). La talla se toma de la última
    // palabra del modelo o del color; los números se comparan como números ("XRE 190" antes que "XRE 300").
    public static class CatalogOrder
    {
        private static readonly string[] Sizes = { "XXS", "XS", "S", "M", "L", "XL", "XXL", "2XL", "XXXL", "3XL", "4XL", "5XL" };
        private static readonly StringComparer Text = StringComparer.Create(new CultureInfo("es-CR"), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        public static List<T> Sort<T>(IEnumerable<T> items, Func<T, int> category, Func<T, string?> name) =>
            items.Select(item => (Item: item, Key: Parse(name(item) ?? string.Empty)))
                 .OrderBy(x => category(x.Item))
                 .ThenBy(x => x.Key.Model, Text)
                 .ThenBy(x => x.Key.Color, Text)
                 .ThenBy(x => x.Key.SizeRank)
                 .ThenBy(x => x.Key.Full, Text)
                 .Select(x => x.Item)
                 .ToList();

        internal static (string Model, string Color, int SizeRank, string Full) Parse(string rawName)
        {
            var name = Spaces.Replace(rawName.Trim(), " ");
            var bar = name.IndexOf('|');
            var model = bar >= 0 ? name[..bar].Trim() : name;
            var color = bar >= 0 ? name[(bar + 1)..].Trim() : string.Empty;

            var size = -1;
            (model, size) = TakeSize(model);
            if (size < 0) (color, size) = TakeSize(color);

            return (Natural(model), Natural(color), size < 0 ? int.MaxValue : size, Natural(name));
        }

        // Quita la talla si es la última palabra y queda algo más antes ("ROJO M" → "ROJO"). Una palabra suelta no es talla.
        private static (string Text, int SizeRank) TakeSize(string text)
        {
            var space = text.LastIndexOf(' ');
            if (space < 0) return (text, -1);
            var index = Array.IndexOf(Sizes, text[(space + 1)..].ToUpperInvariant());
            return index < 0 ? (text, -1) : (text[..space].TrimEnd(), index);
        }

        private static string Natural(string text) => Digits.Replace(text, m => m.Value.PadLeft(8, '0'));
    }
}
