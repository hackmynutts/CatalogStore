using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CatalogStore.BackendAPI.Services.Reports
{
    public enum ColumnKind { Text, Integer, Money, Percent, Date, DateTime }

    // Total = true: la hoja agrega una fila de totales con SUBTOTAL, que respeta los filtros que el usuario aplique en Excel.
    // Width 0 = ancho automático según el contenido.
    public record ReportColumn(string Header, ColumnKind Kind, double Width = 0, bool Total = false);

    // Una hoja del libro. Cada fila es un arreglo con un valor por columna: string, int, decimal, DateTime, DateOnly o null.
    // Percent se guarda como fracción (0.25 = 25 %).
    public class ReportSheet
    {
        public string Name { get; init; } = "Hoja";
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public List<ReportColumn> Columns { get; init; } = new();
        public List<object?[]> Rows { get; init; } = new();
        // Texto debajo de la tabla (aclaraciones, cómo se calculó algo).
        public List<string> Notes { get; init; } = new();
    }

    // Arma un .xlsx con el Open XML SDK (DocumentFormat.OpenXml): sin Excel instalado y sin librerías de terceros.
    public static class ExcelWorkbookBuilder
    {
        private const int HeaderRow = 4;       // filas 1-2: título y subtítulo; la 3 queda en blanco
        private const string Brand = "FF2F5D50";
        private const string Gray = "FF6B7280";

        // Índices de CellFormats (ver BuildStyles; el orden importa).
        private const uint StText = 4, StInteger = 5, StMoney = 6, StPercent = 7, StDate = 8, StDateTime = 9;
        private const uint StTitle = 1, StSubtitle = 2, StHeader = 3, StTotalLabel = 10, StTotalMoney = 11, StTotalInteger = 12, StNote = 13;

        public static byte[] Build(IReadOnlyList<ReportSheet> sheets, string creator)
        {
            using var stream = new MemoryStream();
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                document.PackageProperties.Creator = creator;
                document.PackageProperties.Created = DateTime.UtcNow;

                var workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();
                var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = BuildStyles();

                var sheetsElement = workbookPart.Workbook.AppendChild(new Sheets());
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uint sheetId = 1;
                foreach (var sheet in sheets)
                {
                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    worksheetPart.Worksheet = BuildWorksheet(sheet);
                    sheetsElement.Append(new Sheet
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = sheetId++,
                        Name = UniqueSheetName(sheet.Name, usedNames)
                    });
                }

                // Excel recalcula los SUBTOTAL al abrir (los valores guardados son los de respaldo).
                workbookPart.Workbook.Append(new CalculationProperties { FullCalculationOnLoad = true });
                workbookPart.Workbook.Save();
            }
            return stream.ToArray();
        }

        // ───────── Hoja ─────────

        private static Worksheet BuildWorksheet(ReportSheet sheet)
        {
            var columns = sheet.Columns;
            var sheetData = new SheetData();
            uint rowIndex = 1;

            sheetData.Append(new Row { RowIndex = rowIndex++ }.AppendCell(TextCell(1, 1, sheet.Title, StTitle)));
            sheetData.Append(new Row { RowIndex = rowIndex++ }.AppendCell(TextCell(1, 2, sheet.Subtitle ?? string.Empty, StSubtitle)));
            rowIndex = HeaderRow;

            var header = new Row { RowIndex = rowIndex++, Height = 22, CustomHeight = true };
            for (int c = 0; c < columns.Count; c++)
                header.Append(TextCell(c + 1, HeaderRow, columns[c].Header, StHeader));
            sheetData.Append(header);

            uint firstData = rowIndex;
            foreach (var values in sheet.Rows)
            {
                var row = new Row { RowIndex = rowIndex };
                for (int c = 0; c < columns.Count; c++)
                    row.Append(DataCell(c + 1, rowIndex, c < values.Length ? values[c] : null, columns[c].Kind));
                sheetData.Append(row);
                rowIndex++;
            }
            uint lastData = rowIndex - 1;

            if (sheet.Rows.Count == 0)
            {
                sheetData.Append(new Row { RowIndex = rowIndex }.AppendCell(TextCell(1, (int)rowIndex, "No hay datos para este período.", StNote)));
                rowIndex++;
            }
            else if (columns.Any(c => c.Total))
            {
                var total = new Row { RowIndex = rowIndex };
                for (int c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    if (c == 0) { total.Append(TextCell(1, (int)rowIndex, "Total", StTotalLabel)); continue; }
                    if (!col.Total) { total.Append(TextCell(c + 1, (int)rowIndex, string.Empty, StTotalLabel)); continue; }

                    var letter = ColumnLetter(c + 1);
                    // El valor guardado es el respaldo para visores que no calculan; Excel lo recalcula con SUBTOTAL.
                    var sum = sheet.Rows.Sum(r => c < r.Length ? ToDecimal(r[c]) : 0m);
                    total.Append(new Cell
                    {
                        CellReference = $"{letter}{rowIndex}",
                        StyleIndex = col.Kind == ColumnKind.Money ? StTotalMoney : StTotalInteger,
                        CellFormula = new CellFormula($"SUBTOTAL(109,{letter}{firstData}:{letter}{lastData})"),
                        CellValue = new CellValue(sum.ToString(CultureInfo.InvariantCulture))
                    });
                }
                sheetData.Append(total);
                rowIndex++;
            }

            if (sheet.Notes.Count > 0)
            {
                rowIndex++;
                foreach (var note in sheet.Notes)
                {
                    sheetData.Append(new Row { RowIndex = rowIndex }.AppendCell(TextCell(1, (int)rowIndex, note, StNote)));
                    rowIndex++;
                }
            }

            // Orden de los elementos exigido por el esquema: sheetPr, sheetViews, cols, sheetData, autoFilter, pageMargins, pageSetup.
            var worksheet = new Worksheet();
            worksheet.Append(new SheetProperties(new PageSetupProperties { FitToPage = true }));
            worksheet.Append(new SheetViews(new SheetView(
                new Pane { VerticalSplit = HeaderRow, TopLeftCell = $"A{HeaderRow + 1}", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen },
                new Selection { Pane = PaneValues.BottomLeft })
            { WorkbookViewId = 0 }));
            worksheet.Append(BuildColumns(sheet));
            worksheet.Append(sheetData);
            if (sheet.Rows.Count > 0)
                worksheet.Append(new AutoFilter { Reference = $"A{HeaderRow}:{ColumnLetter(columns.Count)}{lastData}" });
            worksheet.Append(new PageMargins { Left = 0.4, Right = 0.4, Top = 0.6, Bottom = 0.6, Header = 0.3, Footer = 0.3 });
            worksheet.Append(new PageSetup { Orientation = OrientationValues.Landscape, FitToWidth = 1, FitToHeight = 0 });
            return worksheet;
        }

        private static Columns BuildColumns(ReportSheet sheet)
        {
            var element = new Columns();
            for (int c = 0; c < sheet.Columns.Count; c++)
            {
                var col = sheet.Columns[c];
                double width = col.Width;
                if (width <= 0)
                {
                    // Ancho según el texto más largo de las primeras filas; los montos y fechas necesitan un mínimo fijo.
                    int longest = col.Header.Length;
                    foreach (var row in sheet.Rows.Take(300))
                        if (c < row.Length && row[c] is string s) longest = Math.Max(longest, s.Length);
                    width = col.Kind switch
                    {
                        ColumnKind.Money => 16,
                        ColumnKind.DateTime => 17,
                        ColumnKind.Date => 12,
                        ColumnKind.Percent or ColumnKind.Integer => Math.Max(11, col.Header.Length + 2),
                        _ => Math.Clamp(longest + 2, 10, 60)
                    };
                }
                element.Append(new Column { Min = (uint)(c + 1), Max = (uint)(c + 1), Width = width, CustomWidth = true });
            }
            return element;
        }

        // ───────── Celdas ─────────

        private static Cell TextCell(int column, int row, string text, uint style) => new()
        {
            CellReference = $"{ColumnLetter(column)}{row}",
            DataType = CellValues.InlineString,
            StyleIndex = style,
            InlineString = new InlineString(new Text(Clean(text)) { Space = SpaceProcessingModeValues.Preserve })
        };

        private static Cell DataCell(int column, uint row, object? value, ColumnKind kind)
        {
            var reference = $"{ColumnLetter(column)}{row}";
            switch (value)
            {
                case null:
                    return new Cell { CellReference = reference, StyleIndex = StyleFor(kind) };
                case string s:
                    return new Cell { CellReference = reference, StyleIndex = StText, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(Clean(s)) { Space = SpaceProcessingModeValues.Preserve }) };
                case DateTime dt:
                    return Number(reference, dt.ToOADate().ToString(CultureInfo.InvariantCulture), kind == ColumnKind.Date ? StDate : StDateTime);
                case DateOnly d:
                    return Number(reference, d.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture), StDate);
                case bool b:
                    return new Cell { CellReference = reference, StyleIndex = StText, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(b ? "Sí" : "No")) };
                case decimal m:
                    return Number(reference, m.ToString(CultureInfo.InvariantCulture), StyleFor(kind));
                case int or long or short or byte:
                    return Number(reference, Convert.ToString(value, CultureInfo.InvariantCulture)!, StyleFor(kind));
                case double or float:
                    return Number(reference, Convert.ToString(value, CultureInfo.InvariantCulture)!, StyleFor(kind));
                default:
                    return new Cell { CellReference = reference, StyleIndex = StText, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(Clean(value.ToString() ?? string.Empty))) };
            }
        }

        private static Cell Number(string reference, string value, uint style) =>
            new() { CellReference = reference, StyleIndex = style, CellValue = new CellValue(value) };

        private static uint StyleFor(ColumnKind kind) => kind switch
        {
            ColumnKind.Integer => StInteger,
            ColumnKind.Money => StMoney,
            ColumnKind.Percent => StPercent,
            ColumnKind.Date => StDate,
            ColumnKind.DateTime => StDateTime,
            _ => StText
        };

        private static decimal ToDecimal(object? v) => v switch
        {
            decimal m => m, int i => i, long l => l, double d => (decimal)d, float f => (decimal)f, _ => 0m
        };

        private static Row AppendCell(this Row row, Cell cell) { row.Append(cell); return row; }

        // Caracteres de control que el XML no admite (un texto pegado de un PDF puede traerlos) y el tope de Excel por celda.
        private static readonly Regex InvalidXml = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);
        private static string Clean(string text)
        {
            text = InvalidXml.Replace(text ?? string.Empty, " ");
            return text.Length > 32_000 ? text[..32_000] : text;
        }

        public static string ColumnLetter(int index)
        {
            var sb = new StringBuilder();
            while (index > 0)
            {
                index--;
                sb.Insert(0, (char)('A' + index % 26));
                index /= 26;
            }
            return sb.ToString();
        }

        // Excel: máximo 31 caracteres, sin  [ ] : * ? / \  y sin repetir nombres.
        private static string UniqueSheetName(string name, HashSet<string> used)
        {
            var clean = new string((name ?? "Hoja").Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
            if (clean.Length == 0) clean = "Hoja";
            if (clean.Length > 31) clean = clean[..31];
            var candidate = clean;
            for (int n = 2; !used.Add(candidate); n++)
            {
                var suffix = $" ({n})";
                candidate = (clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] : clean) + suffix;
            }
            return candidate;
        }

        // ───────── Estilos ─────────

        private static Stylesheet BuildStyles()
        {
            var numberingFormats = new NumberingFormats(
                new NumberingFormat { NumberFormatId = 164, FormatCode = "\"₡\"#,##0.00;[Red]-\"₡\"#,##0.00" },
                new NumberingFormat { NumberFormatId = 165, FormatCode = "dd/mm/yyyy" },
                new NumberingFormat { NumberFormatId = 166, FormatCode = "dd/mm/yyyy hh:mm" },
                new NumberingFormat { NumberFormatId = 167, FormatCode = "0.0%" });

            var fonts = new Fonts(
                new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),                                                              // 0 normal
                new Font(new Bold(), new FontSize { Val = 15 }, new Color { Rgb = Brand }, new FontName { Val = "Calibri" }),                         // 1 título
                new Font(new Italic(), new FontSize { Val = 10 }, new Color { Rgb = Gray }, new FontName { Val = "Calibri" }),                        // 2 subtítulo / notas
                new Font(new Bold(), new FontSize { Val = 11 }, new Color { Rgb = "FFFFFFFF" }, new FontName { Val = "Calibri" }),                    // 3 encabezado
                new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }));                                                 // 4 totales

            var fills = new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),                                                                    // exigido por Excel
                new Fill(new PatternFill(new ForegroundColor { Rgb = Brand }, new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));

            var light = new Color { Rgb = "FFE5E7EB" };
            var borders = new Borders(
                new Border(),                                                                                                                         // 0 sin borde
                new Border(new LeftBorder(new Color { Rgb = "FFE5E7EB" }) { Style = BorderStyleValues.Thin },
                           new RightBorder(new Color { Rgb = "FFE5E7EB" }) { Style = BorderStyleValues.Thin },
                           new TopBorder(new Color { Rgb = "FFE5E7EB" }) { Style = BorderStyleValues.Thin },
                           new BottomBorder(light) { Style = BorderStyleValues.Thin },
                           new DiagonalBorder()),                                                                                                     // 1 línea suave
                new Border(new LeftBorder(), new RightBorder(), new TopBorder(new Color { Rgb = "FF1F2937" }) { Style = BorderStyleValues.Medium }, new BottomBorder(), new DiagonalBorder())); // 2 total

            CellFormat Fmt(uint numFmt, uint font, uint fill, uint border, HorizontalAlignmentValues? h = null, bool wrap = false)
            {
                var f = new CellFormat
                {
                    NumberFormatId = numFmt, FontId = font, FillId = fill, BorderId = border,
                    ApplyNumberFormat = numFmt != 0, ApplyFont = true, ApplyFill = fill != 0, ApplyBorder = border != 0
                };
                if (h is not null || wrap)
                {
                    f.Alignment = new Alignment { Vertical = VerticalAlignmentValues.Center, WrapText = wrap };
                    if (h is not null) f.Alignment.Horizontal = h;
                    f.ApplyAlignment = true;
                }
                return f;
            }

            var cellFormats = new CellFormats(
                Fmt(0, 0, 0, 0),                                                          // 0 por defecto
                Fmt(0, 1, 0, 0),                                                          // 1 título
                Fmt(0, 2, 0, 0),                                                          // 2 subtítulo
                Fmt(0, 3, 2, 1, HorizontalAlignmentValues.Center, wrap: true),            // 3 encabezado
                Fmt(0, 0, 0, 1),                                                          // 4 texto
                Fmt(3, 0, 0, 1),                                                          // 5 entero #,##0
                Fmt(164, 0, 0, 1),                                                        // 6 monto
                Fmt(167, 0, 0, 1),                                                        // 7 porcentaje
                Fmt(165, 0, 0, 1, HorizontalAlignmentValues.Center),                      // 8 fecha
                Fmt(166, 0, 0, 1, HorizontalAlignmentValues.Center),                      // 9 fecha y hora
                Fmt(0, 4, 0, 2),                                                          // 10 "Total"
                Fmt(164, 4, 0, 2),                                                        // 11 total monto
                Fmt(3, 4, 0, 2),                                                          // 12 total entero
                Fmt(0, 2, 0, 0));                                                         // 13 nota

            return new Stylesheet(numberingFormats, fonts, fills, borders, cellFormats);
        }
    }
}
