# Asistente para importar fotos del catálogo (ver README.md de esta carpeta).
#   .\importar-imagenes.ps1                 → corre todo el proceso guiado
#   .\importar-imagenes.ps1 -Configurar     → vuelve a preguntar rutas, correo y URL de la API
#
# La primera vez pregunta las rutas y las guarda en importar-imagenes.local.json (ignorado por git):
# así cada desarrollador usa sus propias carpetas sin tocar el script.
param(
    [switch]$Configurar,
    [switch]$Insecure
)

$ErrorActionPreference = "Stop"
$toolDir = $PSScriptRoot
$project = Join-Path $toolDir "ImportImages.csproj"
$configPath = Join-Path $toolDir "importar-imagenes.local.json"

function Ask([string]$label, [string]$default) {
    $suffix = if ($default) { " [$default]" } else { "" }
    $value = Read-Host "$label$suffix"
    if ([string]::IsNullOrWhiteSpace($value)) { return $default }
    return $value.Trim().Trim('"')
}

function Pause-Step([string]$message) {
    Write-Host ""
    Read-Host "$message  (Enter para continuar)" | Out-Null
}

function Run-Tool([string[]]$toolArgs) {
    # Start-Process deja la consola directa (la contraseña se pide sin eco y los avisos aparecen al instante).
    # En PowerShell 5.1 hay que entrecomillar cada argumento a mano porque las rutas tienen espacios.
    $all = @("run", "--project", $project, "-c", "Release", "--") + $toolArgs
    $quoted = $all | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }
    $p = Start-Process -FilePath "dotnet" -ArgumentList $quoted -NoNewWindow -Wait -PassThru
    return $p.ExitCode
}

# ── Configuración ──
$config = $null
if ((Test-Path $configPath) -and -not $Configurar) {
    $config = Get-Content $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
}
if ($null -eq $config) {
    Write-Host "Configuración inicial (se guarda en importar-imagenes.local.json)" -ForegroundColor Cyan
    $config = [pscustomobject]@{
        Images     = Ask "Carpeta con las fotos" ""
        Out        = Ask "Carpeta donde dejar la revisión (CSV y página HTML)" ""
        Email      = Ask "Correo del usuario Admin/AdminIT" ""
        Api        = Ask "URL de la API" "https://localhost:7133"
        Connection = Ask "Cadena de conexión SQL (vacío = base local con autenticación de Windows)" ""
    }
    $config | ConvertTo-Json | Set-Content $configPath -Encoding UTF8
}
if (-not (Test-Path $config.Images)) { Write-Host "No existe la carpeta de fotos: $($config.Images)" -ForegroundColor Red; exit 1 }
New-Item -ItemType Directory -Force $config.Out | Out-Null
$reviewCsv = Join-Path $config.Out "imagenes_revision.csv"
$reviewHtml = Join-Path $config.Out "revision_imagenes.html"

# ── Paso 1: proponer producto para cada foto ──
Write-Host ""
Write-Host "Paso 1/4 · Buscando el producto de cada foto…" -ForegroundColor Cyan
$started = Get-Date
$matchArgs = @("match", "--images", $config.Images, "--out", $config.Out)
if ($config.Connection) { $matchArgs += @("--connection", $config.Connection) }
if ((Run-Tool $matchArgs) -ne 0) { Write-Host "Falló el paso 1." -ForegroundColor Red; exit 1 }

# ── Paso 2: revisar con las fotos ──
Write-Host ""
Write-Host "Paso 2/4 · Revisión" -ForegroundColor Cyan
Write-Host "Se abre la página de revisión. Revisá los filtros 'Revisar' y 'Sin coincidencia',"
Write-Host "y al terminar hacé clic en 'Descargar CSV'."
Start-Process $reviewHtml
Pause-Step "Cuando hayas descargado el CSV"

# El navegador lo guarda en Descargas (a veces como 'imagenes_revision (1).csv'): se toma el más nuevo.
$downloads = Join-Path $env:USERPROFILE "Downloads"
$downloaded = Get-ChildItem $downloads -Filter "imagenes_revision*.csv" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -gt $started } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($downloaded) {
    Move-Item $downloaded.FullName $reviewCsv -Force
    Write-Host "Revisión tomada de Descargas: $($downloaded.Name) → $reviewCsv" -ForegroundColor Green
} else {
    Write-Host "No encontré un CSV nuevo en $downloads." -ForegroundColor Yellow
    $answer = Read-Host "¿Usar el que está en la carpeta de revisión (por ejemplo, editado en Excel)? (s/n)"
    if ($answer -notmatch '^[sS]') { Write-Host "Cancelado. Volvé a correr el script cuando tengas el CSV."; exit 0 }
}

# ── Paso 3: prueba ──
Write-Host ""
Write-Host "Paso 3/4 · Prueba (no sube nada)" -ForegroundColor Cyan
try { Invoke-WebRequest $config.Api -UseBasicParsing -TimeoutSec 5 | Out-Null } catch {
    if (-not $_.Exception.Response) {
        Write-Host "La API no responde en $($config.Api). Levantala con:" -ForegroundColor Yellow
        Write-Host "  cd <repo>\src\CatalogStore\CatalogStore.BackendAPI ; dotnet run --launch-profile https"
        Pause-Step "Cuando la API esté corriendo"
    }
}
$uploadArgs = @("upload", "--csv", $reviewCsv, "--images", $config.Images, "--email", $config.Email, "--api", $config.Api)
if ($Insecure) { $uploadArgs += "--insecure" }
[void](Run-Tool ($uploadArgs + "--dry-run"))

# ── Paso 4: subida real ──
Write-Host ""
$confirm = Read-Host "Paso 4/4 · ¿Subir las fotos de verdad? (s/n)"
if ($confirm -notmatch '^[sS]') { Write-Host "No se subió nada."; exit 0 }
$code = Run-Tool $uploadArgs
Write-Host ""
if ($code -eq 0) { Write-Host "Listo. Revisá el catálogo en la UI." -ForegroundColor Green }
else { Write-Host "Terminó con errores: revisá imagenes_subida_log.csv en $($config.Out)." -ForegroundColor Yellow }
