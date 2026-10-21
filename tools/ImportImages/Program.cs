using ImportImages;

// Herramienta para asignar las fotos del catálogo a los productos y subirlas.
//
// 1) match  → propone un producto por archivo (solo lee la base) y genera, en --out:
//      revision_imagenes.html   página para revisar viendo cada foto (abrir con doble clic)
//      imagenes_revision.csv    la misma revisión en CSV (Excel)
//      productos_sin_imagen.csv productos activos que quedarían sin foto
//
//    dotnet run -- match --images "<carpeta de imágenes>" [--out "<carpeta>"] [--connection "<cadena SQL>"] [--previous "<csv>"]
//
//    Si en --out ya existe imagenes_revision.csv (o se indica --previous), se conservan sus decisiones SUBIR y OMITIR.
//
// 2) upload → sube las filas con Accion = SUBIR por la API (pide la contraseña al correr; no se guarda).
//
//    dotnet run -- upload --csv "<imagenes_revision.csv>" --images "<carpeta>" --email <admin>
//                         [--api https://localhost:7133] [--dry-run] [--force] [--insecure]
//
//    --dry-run   muestra lo que subiría sin subir nada
//    --force     sube aunque el producto ya tenga imágenes
//    --insecure  acepta un certificado HTTPS de desarrollo no confiable (solo localhost)
//
// Acciones del CSV: SUBIR (se sube), REVISAR / SIN_COINCIDENCIA (pendientes: no se suben), OMITIR, YA_TIENE_IMAGEN.

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
bool Flag(string name) => args.Contains(name);

var command = args.FirstOrDefault();
var imagesDir = Arg("--images");

if (command is not ("match" or "upload"))
{
    Console.WriteLine("Comandos: match | upload. Ver los comentarios de Program.cs para las opciones.");
    return 1;
}
if (imagesDir is null || !Directory.Exists(imagesDir))
{
    Console.WriteLine($"No existe la carpeta de imágenes: {imagesDir}");
    return 1;
}

if (command == "match")
    return await MatchCommand.RunAsync(imagesDir, Arg("--out") ?? Directory.GetCurrentDirectory(),
        Arg("--connection") ?? MatchCommand.DefaultConnection, Arg("--previous"));

var csv = Arg("--csv");
var email = Arg("--email");
if (csv is null || !File.Exists(csv) || string.IsNullOrWhiteSpace(email))
{
    Console.WriteLine("upload necesita --csv <archivo existente> y --email <usuario admin>.");
    return 1;
}
return await UploadCommand.RunAsync(csv, imagesDir, Arg("--api") ?? "https://localhost:7133", email, Flag("--dry-run"), Flag("--force"), Flag("--insecure"));
