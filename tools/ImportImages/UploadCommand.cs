using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ImportImages;

// Paso 2: sube las filas SUBIR del CSV revisado usando la API (POST api/ProductImage/{productId}),
// la misma que usa la pantalla de imágenes: carpeta por producto, nombre único y registro en la bitácora.
// Se salta los productos que YA tenían imagen antes de empezar (así se puede volver a correr sin duplicar);
// si un mismo producto recibe dos archivos en esta corrida, se suben los dos.
public static class UploadCommand
{
    public static async Task<int> RunAsync(string csvPath, string imagesDir, string apiBase, string email, bool dryRun, bool force, bool insecure)
    {
        var rows = Csv.Read(csvPath)
            .Where(r => string.Equals(r.GetValueOrDefault("Accion")?.Trim(), "SUBIR", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var jobs = new List<(string File, int ProductId)>();
        var problems = new List<string[]>();

        foreach (var r in rows)
        {
            var file = r.GetValueOrDefault("Archivo")?.Trim() ?? "";
            if (!File.Exists(Path.Combine(imagesDir, file)))
            {
                problems.Add(new[] { file, "", "ERROR", "El archivo no existe en la carpeta de imágenes" });
                continue;
            }
            var ids = (r.GetValueOrDefault("ProductIDs") ?? "")
                .Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0).ToList();
            if (ids.Count == 0 || ids.Any(id => id <= 0))
            {
                problems.Add(new[] { file, r.GetValueOrDefault("ProductIDs") ?? "", "ERROR", "ProductIDs vacío o inválido" });
                continue;
            }
            foreach (var id in ids.Distinct())
                jobs.Add((file, id));
        }

        Console.WriteLine($"Filas SUBIR: {rows.Count} → {jobs.Count} subidas (archivo × producto). Problemas en el CSV: {problems.Count}");
        if (jobs.Count == 0)
        {
            WriteLog(csvPath, problems);
            return problems.Count > 0 ? 1 : 0;
        }

        Console.Write($"Contraseña de {email}: ");
        var password = ReadPassword();

        var handler = new HttpClientHandler();
        if (insecure)
        {
            // Solo para pruebas locales con un certificado de desarrollo no confiable.
            var host = new Uri(apiBase).Host;
            if (host is not ("localhost" or "127.0.0.1")) { Console.WriteLine("--insecure solo se permite contra localhost."); return 1; }
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        using var http = new HttpClient(handler) { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(2) };

        var login = await http.PostAsJsonAsync("api/User/login", new { email, password });
        password = string.Empty;
        if (!login.IsSuccessStatusCode)
        {
            Console.WriteLine($"No se pudo iniciar sesión ({(int)login.StatusCode}). Revisá el correo, la contraseña y que la API esté corriendo en {apiBase}.");
            WriteLog(csvPath, problems);
            return 1;
        }
        using (var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync()))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("token").GetString());

        // Cuántas imágenes tenía cada producto ANTES de empezar.
        var existing = new Dictionary<int, int>();
        foreach (var id in jobs.Select(j => j.ProductId).Distinct())
        {
            var res = await http.GetAsync($"api/ProductImage/product/{id}");
            if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("La sesión no tiene permiso (se necesita un usuario Admin o AdminIT).");
                return 1;
            }
            existing[id] = res.IsSuccessStatusCode
                ? (await res.Content.ReadFromJsonAsync<List<JsonElement>>())?.Count ?? 0
                : 0;
        }

        var log = new List<string[]>(problems);
        int uploaded = 0, skipped = 0, failed = 0, n = 0;
        foreach (var (file, productId) in jobs)
        {
            n++;
            var prefix = $"[{n}/{jobs.Count}] {file} → producto {productId}";
            if (!force && existing[productId] > 0)
            {
                skipped++;
                log.Add(new[] { file, productId.ToString(), "OMITIDO", $"El producto ya tenía {existing[productId]} imagen(es)" });
                Console.WriteLine($"{prefix}: omitido (ya tenía imagen)");
                continue;
            }
            if (dryRun)
            {
                uploaded++;
                log.Add(new[] { file, productId.ToString(), "PRUEBA", "Se subiría" });
                Console.WriteLine($"{prefix}: se subiría");
                continue;
            }

            try
            {
                var path = Path.Combine(imagesDir, file);
                using var content = new MultipartFormDataContent();
                var bytes = new ByteArrayContent(await File.ReadAllBytesAsync(path));
                bytes.Headers.ContentType = new MediaTypeHeaderValue(ContentType(path));
                content.Add(bytes, "image", file);

                var res = await http.PostAsync($"api/ProductImage/{productId}", content);
                var body = await res.Content.ReadAsStringAsync();
                var imageId = 0;
                if (res.IsSuccessStatusCode && body.Length > 0)
                    try { using var d = JsonDocument.Parse(body); if (d.RootElement.ValueKind == JsonValueKind.Object && d.RootElement.TryGetProperty("productImageID", out var pid)) imageId = pid.GetInt32(); } catch (JsonException) { }

                if (res.IsSuccessStatusCode && imageId > 0)
                {
                    uploaded++;
                    log.Add(new[] { file, productId.ToString(), "SUBIDO", $"Imagen {imageId}" });
                    Console.WriteLine($"{prefix}: subido (imagen {imageId})");
                }
                else
                {
                    failed++;
                    var detail = res.IsSuccessStatusCode ? "La API no guardó la imagen (¿extensión no permitida?)" : $"HTTP {(int)res.StatusCode} {Truncate(body, 200)}";
                    log.Add(new[] { file, productId.ToString(), "ERROR", detail });
                    Console.WriteLine($"{prefix}: ERROR {detail}");
                    if (res.StatusCode == HttpStatusCode.Unauthorized) { Console.WriteLine("La sesión expiró: volvé a correr el comando (se salta lo ya subido)."); break; }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                failed++;
                log.Add(new[] { file, productId.ToString(), "ERROR", ex.Message });
                Console.WriteLine($"{prefix}: ERROR {ex.Message}");
            }
        }

        var logPath = WriteLog(csvPath, log);
        Console.WriteLine();
        Console.WriteLine(dryRun ? "MODO PRUEBA: no se subió nada." : "Listo.");
        Console.WriteLine($"  {(dryRun ? "Se subirían" : "Subidas")}: {uploaded}   Omitidas (ya tenían imagen): {skipped}   Errores: {failed + problems.Count}");
        Console.WriteLine($"→ {logPath}");
        return failed + problems.Count > 0 ? 1 : 0;
    }

    private static string WriteLog(string csvPath, List<string[]> log)
    {
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(csvPath))!, "imagenes_subida_log.csv");
        Csv.Write(path, new[] { "Archivo", "ProductID", "Resultado", "Detalle" }, log);
        return path;
    }

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // Lee la contraseña sin mostrarla en pantalla.
    private static string ReadPassword()
    {
        if (Console.IsInputRedirected) return (Console.ReadLine() ?? string.Empty).Trim('\uFEFF', '\r', '\n', ' ');
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); continue; }
            if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}
