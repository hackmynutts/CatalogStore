using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ImportImages;

public record Product(int Id, string? Code, string Name, int ImageCount)
{
    public bool HasImage => ImageCount > 0;
}

// Un grupo es el conjunto de productos que comparten el mismo nombre normalizado sin talla
// (ej. CAPA PVC S / M / L / XL → "CAPA PVC"): una misma foto sirve para todas las tallas.
public record ProductGroup(string Key, List<Product> Products)
{
    public List<string> Canon { get; } = NameMatcher.Canonical(Key);
    public HashSet<string> Tokens => Canon.ToHashSet();
    public string Compact => string.Concat(Canon);
}

public record Candidate(ProductGroup Group, double Score, string Differences);

public static partial class NameMatcher
{
    private static readonly HashSet<string> Sizes = new() { "XS", "S", "M", "L", "XL", "XXL", "XXXL", "2XL", "3XL", "4XL" };

    // Palabras que no distinguen un producto de otro ("COLA DE PATO" = "COLA PATO").
    private static readonly HashSet<string> StopWords = new() { "DE", "DEL", "LA", "EL", "LOS", "LAS", "CON", "PARA", "EN", "TALLA" };

    // Colores en una sola forma: ROSADO/ROSADA → ROSA, NEGRA → NEGRO, etc.
    private static readonly Dictionary<string, string> Colors = new()
    {
        ["ROSADO"] = "ROSA", ["ROSADA"] = "ROSA", ["NEGRA"] = "NEGRO", ["BLANCA"] = "BLANCO", ["ROJA"] = "ROJO",
        ["MORADA"] = "MORADO", ["AMARILLA"] = "AMARILLO", ["ANARANJADO"] = "NARANJA"
    };

    // Mayúsculas, sin tildes, sin extensión (también dobles como ".jpg.png"), separadores comunes → espacio,
    // la conjunción "Y" se ignora ("FZ Y PULSAR" = "FZ/PULSAR") y los espacios se colapsan.
    public static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        var s = sb.ToString().ToUpperInvariant();
        s = Extensions().Replace(s, "");
        s = Separators().Replace(s, " ");
        s = StandaloneY().Replace(s, " ");
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Nombre del producto sin las tallas del final: "CAPA SAM MUJER | 2XL" → "CAPA SAM MUJER".
    public static string GroupKey(string productName)
    {
        var tokens = Normalize(productName).Split(' ').ToList();
        while (tokens.Count > 1 && (Sizes.Contains(tokens[^1]) || tokens[^1] == "TALLA"))
            tokens.RemoveAt(tokens.Count - 1);
        return string.Join(' ', tokens);
    }

    // Forma canónica para comparar: sin palabras vacías, colores unificados y sin plural (GUANTES = GUANTE).
    public static List<string> Canonical(string normalizedKey) =>
        normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t))
            .Select(t => Colors.TryGetValue(t, out var c) ? c : t)
            .Select(Singular)
            .ToList();

    private static string Singular(string t)
    {
        if (t.Length <= 3 || t.Any(char.IsDigit)) return t;
        // DESLIZADORES → DESLIZADOR, PEDALES → PEDAL; pero IMPERMEABLES → IMPERMEABLE y GUANTES → GUANTE.
        if (t.EndsWith("ES") && t.Length > 5 && "RLND".Contains(t[^3]) && !t.EndsWith("BLES")) return t[..^2];
        if (t.EndsWith('S')) return t[..^1];
        return t;
    }

    private static readonly HashSet<string> ColorWords = new()
    {
        "AZUL", "NEGRO", "BLANCO", "ROJO", "ROSA", "MORADO", "AMARILLO", "NARANJA", "VERDE", "GRIS", "CELESTE",
        "TURQUESA", "MENTA", "LIMA", "OSCURO", "CLARO", "LAPIZ", "PLATA", "PLATEADO", "DORADO", "CAFE", "TRANSPARENTE"
    };

    // Foto genérica sin color ("PEDALES METÁLICOS") frente a productos que solo agregan el color ("PEDAL METALICO | NEGRO").
    public static bool OnlyColorsAdded(string fileKey, ProductGroup group)
    {
        var fileTokens = Canonical(fileKey).ToHashSet();
        if (fileTokens.Count == 0 || fileTokens.Any(ColorWords.Contains) || !fileTokens.IsSubsetOf(group.Tokens)) return false;
        var extra = group.Tokens.Except(fileTokens).ToList();
        return extra.Count > 0 && extra.All(ColorWords.Contains);
    }

    // Igual después de canonizar, sin importar espacios ni orden: "CORTAVIENTOS HONDA BLANCO" = "CORTA VIENTOS HONDA | BLANCO".
    public static bool SameCanonical(string fileKey, ProductGroup group)
    {
        var canon = Canonical(fileKey);
        if (string.Concat(canon) == group.Compact) return true;
        return canon.OrderBy(t => t).SequenceEqual(group.Canon.OrderBy(t => t));
    }

    public static List<ProductGroup> BuildGroups(IEnumerable<Product> products) =>
        products.GroupBy(p => GroupKey(p.Name))
                .Select(g => new ProductGroup(g.Key, g.OrderBy(p => p.Name).ToList()))
                .ToList();

    // Puntaje 0..1: mitad parecido de letras (Levenshtein), mitad palabras en común (Dice).
    public static double Score(string fileKey, ProductGroup group)
    {
        // Letras sin espacios (así CORTAVIENTOS ≈ CORTA VIENTOS) y palabras canónicas en común.
        var canon = Canonical(fileKey);
        var compact = string.Concat(canon);
        var lev = 1.0 - (double)Levenshtein(compact, group.Compact) / Math.Max(1, Math.Max(compact.Length, group.Compact.Length));
        var fileTokens = canon.ToHashSet();
        var groupTokens = group.Tokens;
        var common = fileTokens.Intersect(groupTokens).Count();
        var dice = fileTokens.Count + groupTokens.Count == 0 ? 0 : 2.0 * common / (fileTokens.Count + groupTokens.Count);
        return 0.6 * lev + 0.4 * dice;
    }

    // Palabras que sobran o faltan, para que quien revisa vea por qué no es exacto: "-XTZ +YBR".
    public static string Differences(string fileKey, ProductGroup group)
    {
        var fileTokens = Canonical(fileKey).ToHashSet();
        var groupTokens = group.Tokens;
        var missing = fileTokens.Except(groupTokens).Select(t => "-" + t);
        var extra = groupTokens.Except(fileTokens).Select(t => "+" + t);
        return string.Join(' ', missing.Concat(extra));
    }

    public static List<Candidate> TopCandidates(string fileKey, IEnumerable<ProductGroup> groups, int take, double minScore) =>
        groups.Select(g => new Candidate(g, Score(fileKey, g), Differences(fileKey, g)))
              .Where(c => c.Score >= minScore)
              .OrderByDescending(c => c.Score)
              .Take(take)
              .ToList();

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    [GeneratedRegex(@"(\.(JPG|JPEG|PNG|WEBP))+$")]
    private static partial Regex Extensions();

    [GeneratedRegex(@"[/|_\-,()\.]")]
    private static partial Regex Separators();

    [GeneratedRegex(@"\bY\b")]
    private static partial Regex StandaloneY();
}
