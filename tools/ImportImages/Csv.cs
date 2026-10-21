using System.Text;

namespace ImportImages;

// CSV para Excel en español: separador ';' y UTF-8 con BOM (sin BOM, Excel rompe las tildes y la Ñ).
public static class Csv
{
    public static void Write(string path, string[] header, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', header.Select(Field)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(';', r.Select(Field)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    // Lee el CSV aunque Excel lo haya guardado con ',' en vez de ';' (depende de la configuración regional).
    public static List<Dictionary<string, string>> Read(string path)
    {
        var text = File.ReadAllText(path); // detecta el BOM UTF-8
        var lines = SplitRecords(text);
        if (lines.Count == 0) return new();
        var separator = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var header = ParseLine(lines[0], separator);
        return lines.Skip(1)
            .Where(l => l.Trim().Length > 0)
            .Select(l =>
            {
                var values = ParseLine(l, separator);
                return header.Select((h, i) => (h.Trim(), i < values.Count ? values[i] : ""))
                             .ToDictionary(x => x.Item1, x => x.Item2, StringComparer.OrdinalIgnoreCase);
            })
            .ToList();
    }

    private static string Field(string v) =>
        v.IndexOfAny(new[] { ';', ',', '"', '\n', '\r' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

    // Separa registros respetando saltos de línea dentro de comillas.
    private static List<string> SplitRecords(string text)
    {
        var records = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            if ((c == '\n') && !inQuotes)
            {
                records.Add(sb.ToString().TrimEnd('\r'));
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) records.Add(sb.ToString().TrimEnd('\r'));
        return records;
    }

    private static List<string> ParseLine(string line, char separator)
    {
        var values = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == separator) { values.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        values.Add(sb.ToString());
        return values;
    }
}
