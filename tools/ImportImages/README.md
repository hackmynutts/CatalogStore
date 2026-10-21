# ImportImages

Herramienta de consola para asignar fotos del catálogo a los productos de CatalogStore y subirlas.
Los archivos se llaman como el producto (por ejemplo `PUÑO PROTAPER AZUL.png`), pero los nombres no siempre
coinciden con los del sistema: la herramienta propone el producto de cada foto, se revisa viendo las fotos y
después se suben por la API.

## Requisitos

- .NET SDK 10.
- Acceso a la base de CatalogStore (por defecto `localhost\SQLEXPRESS`, autenticación de Windows). Solo se lee.
- La API corriendo (por defecto `https://localhost:7133`) y un usuario **Admin** o **AdminIT** para subir.

## Uso rápido (asistente)

Desde esta carpeta:

```powershell
.\importar-imagenes.ps1
```

La primera vez pregunta la carpeta de fotos, la carpeta de salida, el correo admin y la URL de la API, y los
guarda en `importar-imagenes.local.json` (ignorado por git). Para cambiarlos: `.\importar-imagenes.ps1 -Configurar`.

El asistente hace los cuatro pasos:

1. **Propone** un producto para cada foto.
2. **Abre la página de revisión.** Al terminar, "Descargar CSV": el asistente toma el archivo de Descargas.
3. **Prueba** la subida sin subir nada.
4. **Sube** de verdad, si confirmás.

Pide la contraseña en los pasos 3 y 4. No se ve al escribirla y no se guarda en ningún lado.
Si la API usa un certificado de desarrollo no confiable: `.\importar-imagenes.ps1 -Insecure` (solo localhost).

## Uso manual

```powershell
# 1. Proponer
dotnet run --project tools/ImportImages -- match --images "<carpeta de fotos>" --out "<carpeta de salida>"

# 2. Revisar: abrir <salida>\revision_imagenes.html, "Descargar CSV" y guardarlo como <salida>\imagenes_revision.csv

# 3. Prueba
dotnet run --project tools/ImportImages -- upload --csv "<salida>\imagenes_revision.csv" --images "<carpeta de fotos>" --email <admin> --dry-run

# 4. Subir
dotnet run --project tools/ImportImages -- upload --csv "<salida>\imagenes_revision.csv" --images "<carpeta de fotos>" --email <admin>
```

| Opción | Comando | Para qué |
|---|---|---|
| `--connection "<cadena>"` | match | Otra base (por ejemplo, la del servidor) |
| `--previous "<csv>"` | match | Revisión anterior a respetar (por defecto, la que ya esté en `--out`) |
| `--api <url>` | upload | Otra API (por ejemplo, `https://dominio`) |
| `--dry-run` | upload | Muestra lo que subiría sin subir nada |
| `--force` | upload | Sube aunque el producto ya tenga imágenes |
| `--insecure` | upload | Acepta un certificado HTTPS de desarrollo (solo localhost) |

## Archivos que genera (en la carpeta de salida)

| Archivo | Contenido |
|---|---|
| `revision_imagenes.html` | Página para revisar viendo cada foto. Se abre con doble clic; los cambios se guardan en ese navegador |
| `imagenes_revision.csv` | La revisión. Es lo que lee `upload`. También se puede editar en Excel (separador `;`) |
| `imagenes_revision.anterior.csv` | Copia de la revisión anterior, antes de regenerarla |
| `productos_sin_imagen.csv` | Productos activos que quedan sin foto: la lista para pedir fotos nuevas |
| `imagenes_subida_log.csv` | Resultado de cada subida: SUBIDO, OMITIDO, PRUEBA o ERROR |

## Acciones del CSV

| Acción | Significado | ¿Se sube? |
|---|---|---|
| `SUBIR` | Confirmado | Sí |
| `REVISAR` | Coincidencia parecida: confirmar o corregir | No |
| `SIN_COINCIDENCIA` | No se encontró producto: elegirlo a mano | No |
| `OMITIR` | La foto no se usa | No |
| `YA_TIENE_IMAGEN` | Todos sus productos ya tienen foto | No |

## Cómo propone el producto

Compara el nombre del archivo con el nombre del producto después de normalizar ambos: mayúsculas, sin tildes,
sin extensión (también `.jpg.png`), `/`, `-`, `|` y la palabra `Y` como separadores.

- **Exacto:** mismo nombre. Las tallas del final (`S`, `M`, `L`, `XL`, `2XL`…) se ignoran, así una foto sirve para todas las tallas.
- **Exacto normalizado:** igual salvo espacios (`CORTAVIENTOS` = `CORTA VIENTOS`), orden de palabras,
  palabras como `DE`/`CON`, plural (`GUANTES` = `GUANTE`) o género del color (`ROJA` = `ROJO`, `ROSADO` = `ROSA`).
- **Genérica sin color:** la foto no dice color y los productos solo agregan el color (`PEDALES METÁLICOS` →
  `PEDAL METALICO | NEGRO`, `| ROJO`…). Queda en REVISAR por si cada color tiene su propia foto.
- **Parecido:** puntaje de similitud ≥ 0.75. Siempre queda en REVISAR, con las palabras que difieren
  (`-XTZ +YBR`): los modelos que difieren en una palabra se confunden fácil.

Muchas fotos traen impreso el **código del producto**, que coincide con `ProductCode`. La página de revisión
muestra el código de cada candidato para comparar, y el buscador acepta códigos.

## Volver a usarla (fotos nuevas)

1. Copiar las fotos nuevas a la misma carpeta.
2. Correr el asistente (o `match`) otra vez.
   - Las decisiones `SUBIR` y `OMITIR` de la revisión anterior se conservan.
   - Las fotos cuyos productos ya tienen imagen salen como `YA_TIENE_IMAGEN`.
   - Solo hay que revisar las fotos nuevas.
3. `upload` se puede correr varias veces: se salta los productos que ya tenían foto antes de empezar.
   Si se corta, se vuelve a correr y sigue.

## En el servidor

Las imágenes quedan en `CatalogStore.BackendAPI/wwwroot/images/products/<ProductID>/`. Al publicar, hay dos opciones:

- Copiar esa carpeta junto con la base.
- Correr `upload` contra la API del servidor con `--api https://dominio`, y `match` con `--connection` apuntando a esa base.

## Problemas comunes

| Mensaje | Qué hacer |
|---|---|
| `No se pudo iniciar sesión (401)` | Revisar correo y contraseña, y que la API esté corriendo en la URL indicada |
| `La sesión no tiene permiso` | El usuario tiene que ser Admin o AdminIT |
| Error de certificado HTTPS | Agregar `--insecure` (solo contra localhost) o confiar el certificado con `dotnet dev-certs https --trust` |
| `El archivo no existe en la carpeta de imágenes` | El CSV menciona una foto que se renombró o borró |
| La página de revisión muestra decisiones viejas | "Restablecer" borra lo guardado en el navegador |
