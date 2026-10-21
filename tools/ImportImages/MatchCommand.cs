using Microsoft.Data.SqlClient;

namespace ImportImages;

public record CandidateView(List<int> Ids, string Name, double Score, string Differences);

public record ReviewRow(
    string File, string Action, string Match, List<int> ProductIds, string ProductNames,
    List<CandidateView> Candidates, string Note);

// Paso 1: propone un producto para cada archivo. Solo lee la base (autenticación de Windows).
public static class MatchCommand
{
    public const string DefaultConnection = "Server=localhost\\SQLEXPRESS;Database=CatalogStoreDB;Trusted_Connection=True;TrustServerCertificate=True";
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    public static readonly string[] Header = { "Archivo", "Accion", "Coincidencia", "ProductIDs", "ProductosEnSistema", "OtrosCandidatos", "Nota" };

    public static async Task<int> RunAsync(string imagesDir, string outDir, string connectionString, string? previousCsv)
    {
        Directory.CreateDirectory(outDir);
        var products = await LoadProductsAsync(connectionString);
        var groups = NameMatcher.BuildGroups(products);
        var groupsByKey = groups.ToDictionary(g => g.Key);

        var files = Directory.GetFiles(imagesDir)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<ReviewRow>();
        var assigned = new Dictionary<int, List<string>>(); // producto → archivos que lo proponen (SUBIR/REVISAR)

        foreach (var path in files)
        {
            var file = Path.GetFileName(path);
            var key = NameMatcher.Normalize(file);
            var candidates = NameMatcher.TopCandidates(key, groups, take: 4, minScore: 0.55);
            var notes = new List<string>();
            ProductGroup? group = null;
            string match, action;

            if (groupsByKey.TryGetValue(key, out var exact))
            {
                group = exact;
                match = exact.Products.Count > 1 ? $"exacto ({exact.Products.Count} productos)" : "exacto";
                action = "SUBIR";
            }
            else if (groups.Where(g => NameMatcher.SameCanonical(key, g)).ToList() is { Count: > 0 } same)
            {
                // Igual salvo espacios, orden, "DE", plural o género del color. Si hay más de un grupo así, se revisa.
                group = same[0];
                match = same.Count == 1 ? "exacto (normalizado)" : $"exacto (normalizado, {same.Count} grupos)";
                action = same.Count == 1 ? "SUBIR" : "REVISAR";
            }
            else if (groups.Where(g => NameMatcher.OnlyColorsAdded(key, g)).ToList() is { Count: > 0 } colorGroups)
            {
                // Foto genérica para todas las variantes de color: se revisa, porque puede que cada color tenga su foto.
                group = new ProductGroup(key, colorGroups.SelectMany(g => g.Products).ToList());
                match = $"genérica sin color ({colorGroups.Count} variantes)";
                action = "REVISAR";
            }
            else if (candidates.Count > 0 && candidates[0].Score >= 0.75)
            {
                group = candidates[0].Group;
                match = $"parecido {candidates[0].Score:0.00}";
                action = "REVISAR";
                notes.Add($"Diferencias: {candidates[0].Differences}");
            }
            else
            {
                match = candidates.Count > 0 ? $"mejor parecido {candidates[0].Score:0.00}" : "ninguno";
                action = "SIN_COINCIDENCIA";
            }

            if (group is not null)
            {
                var withImage = group.Products.Count(p => p.HasImage);
                if (withImage == group.Products.Count)
                    action = "YA_TIENE_IMAGEN";
                else if (withImage > 0)
                    notes.Add($"{withImage} de {group.Products.Count} ya tienen imagen (se suben solo los que no)");

                if (group.Products.Count > 1 && group.Products.Select(p => p.Name.Trim().ToUpperInvariant()).Distinct().Count() < group.Products.Count)
                    notes.Add("Hay productos con el mismo nombre (posible duplicado en el sistema)");

                if (action is "SUBIR" or "REVISAR")
                    foreach (var p in group.Products)
                        (assigned.TryGetValue(p.Id, out var list) ? list : assigned[p.Id] = new()).Add(file);
            }

            rows.Add(new ReviewRow(
                file, action, match,
                group?.Products.Select(p => p.Id).ToList() ?? new(),
                group is null ? "" : string.Join(" / ", group.Products.Select(p => p.Name.Trim())),
                candidates.Where(c => c.Group != group)
                          .Select(c => new CandidateView(c.Group.Products.Select(p => p.Id).ToList(), c.Group.Products[0].Name.Trim(), c.Score, c.Differences))
                          .ToList(),
                string.Join(". ", notes)));
        }

        // La revisión anterior manda sobre la propuesta automática: se conservan las decisiones SUBIR y OMITIR
        // de los archivos que siguen en la carpeta. Si todos sus productos ya tienen foto, pasa a YA_TIENE_IMAGEN.
        var kept = KeepPreviousDecisions(rows, products, previousCsv ?? Path.Combine(outDir, "imagenes_revision.csv"), outDir);

        assigned.Clear();
        foreach (var r in rows.Where(r => r.Action is "SUBIR" or "REVISAR"))
            foreach (var id in r.ProductIds)
                (assigned.TryGetValue(id, out var list) ? list : assigned[id] = new()).Add(r.File);

        // Productos propuestos por más de un archivo (fotos distintas del mismo producto, o un error).
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Action is not ("SUBIR" or "REVISAR")) continue;
            var others = row.ProductIds.Where(id => assigned[id].Count > 1)
                .SelectMany(id => assigned[id]).Where(f => f != row.File).Distinct().ToList();
            if (others.Count > 0)
                rows[i] = row with { Note = string.Join(". ", new[] { row.Note, $"También lo propone: {string.Join(", ", others)}" }.Where(s => s.Length > 0)) };
        }

        var reviewCsv = Path.Combine(outDir, "imagenes_revision.csv");
        Csv.Write(reviewCsv, Header, rows.Select(r => new[]
        {
            r.File, r.Action, r.Match, string.Join(",", r.ProductIds), r.ProductNames,
            string.Join(" | ", r.Candidates.Select(c => $"{string.Join(",", c.Ids)} {c.Name} ({c.Score:0.00}; {c.Differences})")),
            r.Note
        }));

        var withoutImage = products.Where(p => !p.HasImage && !assigned.ContainsKey(p.Id)).OrderBy(p => p.Name).ToList();
        var missingCsv = Path.Combine(outDir, "productos_sin_imagen.csv");
        Csv.Write(missingCsv, new[] { "ProductID", "Codigo", "Nombre" },
            withoutImage.Select(p => new[] { p.Id.ToString(), p.Code ?? "", p.Name.Trim() }));

        var reviewHtml = Path.Combine(outDir, "revision_imagenes.html");
        ReviewPage.Write(reviewHtml, Path.GetRelativePath(outDir, imagesDir), rows, products);

        Console.WriteLine($"Productos activos: {products.Count} ({groups.Count} grupos sin talla)");
        Console.WriteLine($"Archivos de imagen: {files.Count}");
        foreach (var a in new[] { "SUBIR", "REVISAR", "SIN_COINCIDENCIA", "YA_TIENE_IMAGEN" })
            Console.WriteLine($"  {a,-17} {rows.Count(r => r.Action == a)}");
        if (kept > 0)
            Console.WriteLine($"Decisiones conservadas de la revisión anterior: {kept} (copia en imagenes_revision.anterior.csv)");
        Console.WriteLine($"Productos que recibirían imagen (SUBIR + REVISAR): {assigned.Count}");
        Console.WriteLine($"Productos que siguen sin imagen: {withoutImage.Count}");
        Console.WriteLine($"→ {reviewHtml}   (abrilo en el navegador para revisar con las fotos)");
        Console.WriteLine($"→ {reviewCsv}");
        Console.WriteLine($"→ {missingCsv}");
        return 0;
    }

    private static int KeepPreviousDecisions(List<ReviewRow> rows, List<Product> products, string previousPath, string outDir)
    {
        if (!File.Exists(previousPath)) return 0;

        var byId = products.ToDictionary(p => p.Id);
        var previous = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Csv.Read(previousPath))
        {
            var file = r.GetValueOrDefault("Archivo")?.Trim() ?? "";
            if (file.Length > 0) previous.TryAdd(file, r);
        }

        var kept = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (!previous.TryGetValue(rows[i].File, out var prev)) continue;
            var prevAction = prev.GetValueOrDefault("Accion")?.Trim().ToUpperInvariant();
            if (prevAction is not ("SUBIR" or "OMITIR")) continue;

            var prevIds = (prev.GetValueOrDefault("ProductIDs") ?? "")
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => int.TryParse(t, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();
            var valid = prevIds.Where(byId.ContainsKey).ToList();
            var missing = prevIds.Except(valid).ToList();
            var names = string.Join(" / ", valid.Select(id => byId[id].Name.Trim()));

            if (prevAction == "OMITIR")
            {
                rows[i] = rows[i] with { Action = "OMITIR", Match = "decisión anterior", ProductIds = valid, ProductNames = names, Note = "" };
                kept++;
                continue;
            }

            if (valid.Count == 0)
            {
                // Los productos elegidos antes ya no existen o están inactivos: se vuelve a revisar con la propuesta nueva.
                rows[i] = rows[i] with { Action = "REVISAR", Note = $"Los productos de la revisión anterior ({string.Join(",", missing)}) ya no existen o están inactivos" };
                continue;
            }

            var action = valid.All(id => byId[id].HasImage) ? "YA_TIENE_IMAGEN" : missing.Count > 0 ? "REVISAR" : "SUBIR";
            var note = missing.Count > 0 ? $"IDs de la revisión anterior que ya no existen o están inactivos: {string.Join(",", missing)}" : "";
            rows[i] = rows[i] with { Action = action, Match = "decisión anterior", ProductIds = valid, ProductNames = names, Note = note };
            kept++;
        }

        File.Copy(previousPath, Path.Combine(outDir, "imagenes_revision.anterior.csv"), overwrite: true);
        return kept;
    }

    public static async Task<List<Product>> LoadProductsAsync(string connectionString)
    {
        var products = new List<Product>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        const string sql = """
            SELECT p.ProductID, p.ProductCode, p.ProductName,
                   (SELECT COUNT(*) FROM dbo.ProductImages_TB i WHERE i.ProductID = p.ProductID) AS ImageCount
            FROM dbo.Products_TB p
            WHERE p.StatusID = 1
            """;
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            products.Add(new Product(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        return products;
    }
}
