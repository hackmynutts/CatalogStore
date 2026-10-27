using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Models.SalesHeader;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CatalogStore.BackendAPI.Services.Invoicing
{
    public record InvoiceBuildInput(
        SalesHeader Sale,            // con líneas y, en cada línea, su producto (CABYS y unidad)
        string? ClientActivity,      // actividad económica del cliente (Client.Cabys, nombre heredado)
        InvoiceDocType DocType,
        int PaymentMethod,           // código de Hacienda: 1, 2, 3, 4, 6, 7, 99
        DateTime LocalNow,           // fecha y hora de emisión, hora de Costa Rica
        string SecurityCode,         // 8 dígitos
        decimal IvaPercent,          // 13
        Invoice? Reference = null,   // nota de crédito: el comprobante que anula
        string? Reason = null);      // nota de crédito: motivo

    public record InvoiceBuildResult(string? Json, List<string> Errors, decimal Subtotal, decimal Iva, decimal Total, int LineCount)
    {
        public bool Ok => Errors.Count == 0 && Json is not null;
    }

    // Convierte una venta en el JSON del proveedor de facturación (manual ApiJsonFacturacion). Es una función pura:
    // no toca la base ni la red, así se prueba sola. Las reglas que importan, del manual:
    //  - los códigos, claves y consecutivos van como texto (se pierden los ceros si son números);
    //  - los montos van como texto con punto decimal (5 decimales), sin símbolos ni separadores;
    //  - el proveedor NO calcula el IVA ni valida los totales: este código tiene que dejarlos cuadrados;
    //  - no se puede usar "|" ni "_" en referencias ni en "otros" (el proveedor los usa como separadores);
    //  - solo se emite si todo lo obligatorio está completo: acá se rechaza antes de enviar nada.
    public static class InvoiceJsonBuilder
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = null, // las claves distinguen mayúsculas: "Fecha_Emision", "clientid"
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        private const decimal Tolerance = 0.005m;

        // Las ventas sin IVA no van a Hacienda: llevan un tiquete de caja (impresión no fiscal). Decisión del negocio 2026-10-27.
        public const string NoIvaMessage = "Las ventas sin IVA no se facturan electrónicamente: usá el tiquete de caja (impresión no fiscal).";

        public static string Money(decimal value) => value.ToString("0.00000", CultureInfo.InvariantCulture);

        public static string DocCode(InvoiceDocType type) => ((int)type).ToString("D2");

        public static string PaymentCode(int method) => method.ToString("D2");

        public static InvoiceBuildResult Build(InvoiceBuildInput input, InvoicingOptions options)
        {
            var sale = input.Sale;
            var errors = new List<string>();

            if (!sale.ApplyIva) errors.Add(NoIvaMessage);
            if (sale.PaymentStatus == PaymentStatus.Voided) errors.Add($"La venta {sale.SaleNumber} está anulada.");
            if (sale.Lines.Count == 0) errors.Add("La venta no tiene productos.");
            if (sale.Lines.Count > 200) errors.Add("El comprobante admite como máximo 200 líneas.");
            if (string.IsNullOrWhiteSpace(sale.ClientName)) errors.Add("La venta no tiene nombre de cliente.");
            if (string.IsNullOrWhiteSpace(sale.ClientIdentification)) errors.Add("La venta no tiene identificación del cliente.");
            if (sale.ClientIdentificationType == IdentificationType.Extranjero)
                errors.Add("Los clientes extranjeros todavía no se pueden facturar desde el sistema.");
            if (input.PaymentMethod is not (1 or 2 or 3 or 4 or 6 or 7 or 99)) errors.Add("Elegí un medio de pago válido.");
            if (input.DocType == InvoiceDocType.NotaCredito && (input.Reference?.Clave is null || string.IsNullOrWhiteSpace(input.Reason)))
                errors.Add("La nota de crédito necesita la clave del comprobante original y un motivo.");
            if (input.Reason is not null && (input.Reason.Contains('|') || input.Reason.Contains('_')))
                errors.Add("El motivo no puede llevar los caracteres | ni _.");

            // Líneas
            var withoutCabys = new List<string>();
            var withoutUnit = new List<string>();
            var lines = new List<object>();
            decimal subtotal = 0m, iva = 0m, discount = 0m;
            int position = 0;
            foreach (var line in sale.Lines.OrderBy(l => l.SalesLineID))
            {
                position++;
                var product = line.Product;
                if (string.IsNullOrWhiteSpace(product?.CabysCode)) withoutCabys.Add(line.ProductName);
                string? unit = null;
                if (product is null || !options.UnitCodes.TryGetValue(product.UnidadMedida.ToString(), out unit) || string.IsNullOrWhiteSpace(unit))
                    withoutUnit.Add(line.ProductName);
                if (line.Quantity <= 0) errors.Add($"{line.ProductName}: la cantidad tiene que ser mayor a cero.");

                var gross = line.UnitPrice * line.Quantity;
                var lineDiscount = Math.Max(gross - line.LineSubtotal, 0m);
                var lineIva = line.LineIva;
                subtotal += line.LineSubtotal;
                iva += lineIva;
                discount += lineDiscount;

                var code = (string.IsNullOrWhiteSpace(line.ProductCode) ? $"P{line.ProductID}" : line.ProductCode.Trim());
                lines.Add(new
                {
                    posicion = position,
                    codigo_cabys = product?.CabysCode ?? string.Empty,
                    codigos = new[] { new { tipo = "04", codigo = Truncate(code, 20) } },
                    cantidad = line.Quantity,
                    codigo_unidad_medida = unit ?? string.Empty,
                    descripcion = Truncate(Clean(line.ProductName), 200),
                    precio_unitario = Money(line.UnitPrice),
                    monto_descuento = Money(lineDiscount),
                    descripcion_descuento = lineDiscount > 0 ? $"Descuento comercial {line.Discount.ToString("0.##", CultureInfo.InvariantCulture)} %" : string.Empty,
                    sub_total = Money(line.LineSubtotal),
                    registro_medicamento = string.Empty,
                    forma_farmaceutica = string.Empty,
                    tipo_transaccion = "01",
                    partida_arancelaria = string.Empty,
                    impuestos = new[]
                    {
                        new
                        {
                            codigo_tipo = "01",          // IVA
                            codigo_tarifa_iva = "08",    // tarifa general
                            tarifa = input.IvaPercent,
                            monto = Money(lineIva),
                            exoneracion = new
                            {
                                codigo_tipo = string.Empty, numero_documento = string.Empty, nombre_institucion = string.Empty,
                                fecha_emision = string.Empty, hora_emision = string.Empty, porcentaje = 0, monto = 0,
                                articulo = string.Empty, inciso = string.Empty, nombre_institucion_otros = string.Empty
                            }
                        }
                    }
                });
            }
            if (withoutCabys.Count > 0) errors.Add($"Falta el CABYS de: {Names(withoutCabys)}. Asignalo en Productos > CABYS.");
            if (withoutUnit.Count > 0) errors.Add($"Falta el código de unidad de: {Names(withoutUnit)}.");

            // El proveedor no valida los totales: se comprueba acá, contra lo que la venta ya tiene guardado.
            var total = subtotal + iva;
            if (Math.Abs(total - sale.TotalAmount) > Tolerance)
                errors.Add($"Los totales no cuadran: las líneas suman ₡{total:N2} y la venta dice ₡{sale.TotalAmount:N2}.");
            if (Math.Abs(discount - sale.DiscountTotal) > Tolerance)
                errors.Add($"El descuento de las líneas (₡{discount:N2}) no coincide con el de la venta (₡{sale.DiscountTotal:N2}).");

            if (errors.Count > 0)
                return new InvoiceBuildResult(null, errors, subtotal, iva, total, lines.Count);

            var isCredit = sale.PaymentTerm != credit.D0 && input.DocType != InvoiceDocType.NotaCredito;
            var phone = new string((sale.ClientPhone ?? string.Empty).Where(char.IsDigit).ToArray());
            var email = (sale.ClientEmail ?? string.Empty).Trim();
            var idCode = ((int)sale.ClientIdentificationType).ToString("D2");

            var references = input.DocType == InvoiceDocType.NotaCredito
                ? new[]
                {
                    new
                    {
                        TipoDoc = DocCode(input.Reference!.DocumentType),
                        Numero = input.Reference.Clave!,
                        Fecha_Emision = IsoLocal(input.Reference.IssuedOn),
                        Codigo = "01", // 01 = anula el documento de referencia
                        Razon = Truncate(Clean(input.Reason!), 180)
                    }
                }
                : Array.Empty<object>().Select(_ => new { TipoDoc = "", Numero = "", Fecha_Emision = "", Codigo = "", Razon = "" }).ToArray();

            var body = new
            {
                encabezado = new
                {
                    cedulaemisor = options.IssuerId,
                    clientid = options.ProfileId!.Value,
                    codigousuario = options.RegisterUser,
                    codigoactividad = options.ActivityCode,
                    codigo_seguridad = input.SecurityCode,
                    codigo_sucursal = options.BranchCode,
                    codigo_terminal = options.TerminalCode,
                    situacion_documento = options.SituationCode
                },
                numero_control = string.Empty, // "" = el proveedor asigna el siguiente consecutivo
                fecha_emision = input.LocalNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                hora_emision = input.LocalNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                codigo_tipo = DocCode(input.DocType),
                codigo_condicion_venta = isCredit ? "02" : "01",
                plazo_credito = isCredit ? PaymentTerms.Days(sale.PaymentTerm) : 0,
                codigos_medios_pago = new[] { PaymentCode(input.PaymentMethod) },
                observaciones = string.Empty,
                observacionesdescuentos = discount > 0 ? "Descuento comercial" : string.Empty,
                codigoobservacionesdescuentos = "07",
                referencia = references,
                otros = new[] { new { Codigo = "01", OtroTexto = Truncate(input.DocType == InvoiceDocType.NotaCredito ? $"Anulacion de la venta {sale.SaleNumber}" : $"Venta {sale.SaleNumber}", 200) } },
                clavenumerica = string.Empty, // en emisión va vacía: con valor el proveedor ejecuta también una consulta
                codigousuario = string.Empty,
                receptor = new
                {
                    nombre = Truncate(Clean(sale.ClientName), 100),
                    identificacion = new { codigo_tipo = idCode, numero = new string(sale.ClientIdentification.Where(char.IsLetterOrDigit).ToArray()) },
                    idrealcliente = string.Empty,
                    codclie = string.Empty,
                    codigoactividad = (input.ClientActivity ?? string.Empty).Trim(),
                    telefono = new { codigo_pais = "506", numero_telefono = Truncate(phone, 20) },
                    fax = new { numero = string.Empty },
                    correo_electronico = email,
                    correos_notificar = string.Empty,
                    // Solo texto libre: no guardamos provincia/cantón/distrito, y sin provincia el proveedor omite la ubicación.
                    ubicacion = new
                    {
                        codigo_provincia = string.Empty, codigo_canton = string.Empty, codigo_distrito = string.Empty,
                        codigo_barrio = string.Empty, otras_senas = Truncate(Clean(sale.ClientAddress ?? string.Empty), 160)
                    },
                    IdentificacionExtranjero = string.Empty,
                    OtrasSenasExtranjero = string.Empty
                },
                elementos = lines,
                resumen = new
                {
                    codigo_moneda = "CRC",
                    tipo_cambio = Money(1m),
                    tipo_cambio_compra = 0,
                    tipo_cambio_venta = 0,
                    total_descuentos = Money(discount),
                    total_exento = Money(0m),
                    total_gravado = Money(subtotal)
                }
            };

            return new InvoiceBuildResult(JsonSerializer.Serialize(body, Json), errors, subtotal, iva, total, lines.Count);
        }

        // Consulta del estado de un comprobante (manual 11.4): solo clientid y cédula emisora vacía.
        public static string BuildStatusQuery(string clave, InvoicingOptions options) =>
            JsonSerializer.Serialize(new
            {
                encabezado = new { clientid = options.ProfileId!.Value, cedulaemisor = string.Empty },
                clavenumerica = clave,
                codigousuario = options.RegisterUser
            }, Json);

        // Fecha-hora original como la espera la referencia: 2026-09-15T10:00:00-06:00 (Costa Rica no usa horario de verano).
        public static string IsoLocal(DateTime utc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), CostaRica);
            return local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "-06:00";
        }

        private static readonly TimeZoneInfo CostaRica = TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica");

        // "|" y "_" rompen el formato interno del proveedor; los saltos de línea no tienen sentido en un XML de una línea.
        private static string Clean(string text) =>
            text.Replace('|', '/').Replace('_', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

        private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

        private static string Names(List<string> names) =>
            names.Count <= 4 ? string.Join(", ", names) : string.Join(", ", names.Take(4)) + $" y {names.Count - 4} más";
    }
}
