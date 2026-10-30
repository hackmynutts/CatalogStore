using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Invoicing;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Repository.Invoices;
using CatalogStore.BackendAPI.Services.Email;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Orders;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CatalogStore.BackendAPI.Services.Invoicing
{
    public class InvoiceServices : IInvoiceServices
    {
        private const string ModuleName = "Ventas/Facturación";
        private const string InvoiceTable = "Invoice";
        public const string NotFoundMessage = "Venta o comprobante no encontrado.";
        private const int MaxStored = 200_000; // tope de texto guardado por respuesta del proveedor
        private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(2);

        // Una emisión a la vez en todo el sistema: el proveedor numera con MAX + 1 sin bloqueo, y dos envíos simultáneos
        // podrían tomar el mismo número. Con más de una instancia de la API hay que cambiarlo por un bloqueo en la base
        // (sp_getapplock) o coordinar las series.
        private static readonly SemaphoreSlim EmitGate = new(1, 1);

        private readonly IInvoiceRepository _repository;
        private readonly IInvoiceTransport _transport;
        private readonly IOptionsMonitor<InvoicingOptions> _options;
        private readonly IOptions<PricingOptions> _pricing;
        private readonly IEventlogServices _eventlogServices;
        private readonly IEmailServices _emailServices;
        private readonly TimeProvider _time;

        public InvoiceServices(IInvoiceRepository repository, IInvoiceTransport transport, IOptionsMonitor<InvoicingOptions> options,
            IOptions<PricingOptions> pricing, IEventlogServices eventlogServices, IEmailServices emailServices, TimeProvider time)
        {
            _repository = repository;
            _transport = transport;
            _options = options;
            _pricing = pricing;
            _eventlogServices = eventlogServices;
            _emailServices = emailServices;
            _time = time;
        }

        // ───────── Lectura ─────────

        public async Task<List<InvoiceDTO>?> GetBySaleAsync(int salesHeaderId, string user, bool isAdmin)
        {
            var sale = await _repository.GetSaleForInvoiceAsync(salesHeaderId);
            if (sale is null || !CanAccess(sale, user, isAdmin)) return null;
            return (await _repository.GetBySaleAsync(salesHeaderId)).Select(Map).ToList();
        }

        public async Task<InvoicePreviewDTO?> PreviewAsync(int salesHeaderId, int? paymentMethod)
        {
            var sale = await _repository.GetSaleForInvoiceAsync(salesHeaderId);
            if (sale is null) return null;

            var options = _options.CurrentValue;
            var docType = DocTypeFor(sale);
            if (!sale.ApplyIva)
                return new InvoicePreviewDTO
                {
                    CanEmit = false, DocumentType = 0, DocumentTypeName = "Tiquete de caja (no fiscal)", Enabled = options.Enabled,
                    Subtotal = sale.Subtotal, Iva = sale.IvaTotal, Total = sale.TotalAmount, LineCount = sale.Lines.Count,
                    Errors = new List<string> { InvoiceJsonBuilder.NoIvaMessage }
                };
            var preview = new InvoicePreviewDTO
            {
                DocumentType = (int)docType,
                DocumentTypeName = docType.Humanize(),
                Enabled = options.Enabled,
                MissingSettings = options.MissingSettings()
            };

            var existing = await _repository.GetActiveAsync(salesHeaderId, docType);
            if (existing is not null)
                preview.Errors.Add($"La venta ya tiene {docType.Humanize().ToLowerInvariant()} ({existing.Status.Humanize().ToLowerInvariant()}).");

            var method = ResolvePaymentMethod(sale, paymentMethod);
            preview.PaymentMethod = method;

            // La vista previa usa un método de pago de relleno para poder revisar el resto aunque todavía no se haya elegido.
            var build = InvoiceJsonBuilder.Build(await NewInputAsync(sale, docType, method ?? 99, null, null, "00000000"), options.ProfileId is null ? WithDummyProfile(options) : options);
            preview.Subtotal = build.Subtotal;
            preview.Iva = build.Iva;
            preview.Total = build.Total;
            preview.LineCount = build.LineCount;
            preview.Errors.AddRange(build.Errors);
            if (method is null) preview.Errors.Add("Elegí el medio de pago: la venta todavía no tiene abonos.");
            if (!options.Enabled) preview.Errors.Add("La facturación electrónica está deshabilitada (Invoicing:Enabled).");
            if (preview.MissingSettings.Count > 0) preview.Errors.Add($"Falta configurar el emisor: {string.Join(", ", preview.MissingSettings)}.");
            preview.CanEmit = preview.Errors.Count == 0;
            return preview;
        }

        // ───────── Emisión ─────────

        public async Task<OperationResult> EmitAsync(int salesHeaderId, EmitInvoiceDTO dto, string user)
        {
            var guard = ConfigError();
            if (guard is not null) return OperationResult.Fail(guard);

            await EmitGate.WaitAsync();
            try
            {
                var sale = await _repository.GetSaleForInvoiceAsync(salesHeaderId);
                if (sale is null) return OperationResult.Fail(NotFoundMessage);

                if (!sale.ApplyIva) return OperationResult.Fail(InvoiceJsonBuilder.NoIvaMessage);
                var docType = DocTypeFor(sale);
                if (await _repository.GetActiveAsync(salesHeaderId, docType) is { } active)
                    return OperationResult.Fail($"La venta ya tiene {docType.Humanize().ToLowerInvariant()} ({active.Status.Humanize().ToLowerInvariant()}).");

                var method = ResolvePaymentMethod(sale, dto.PaymentMethod);
                if (method is null) return OperationResult.Fail("Elegí el medio de pago: la venta todavía no tiene abonos.");

                return await EmitCoreAsync(sale, docType, method.Value, reference: null, reason: null, user);
            }
            finally { EmitGate.Release(); }
        }

        public async Task<OperationResult> EmitCreditNoteAsync(int salesHeaderId, CreditNoteDTO dto, string user)
        {
            var guard = ConfigError();
            if (guard is not null) return OperationResult.Fail(guard);

            await EmitGate.WaitAsync();
            try
            {
                var sale = await _repository.GetSaleForInvoiceAsync(salesHeaderId);
                if (sale is null) return OperationResult.Fail(NotFoundMessage);
                if (sale.PaymentStatus == PaymentStatus.Voided) return OperationResult.Fail($"La venta {sale.SaleNumber} ya está anulada.");
                if (!sale.ApplyIva) return OperationResult.Fail(InvoiceJsonBuilder.NoIvaMessage);

                var original = await _repository.GetActiveAsync(salesHeaderId, DocTypeFor(sale));
                if (original is null) return OperationResult.Fail("La venta no tiene factura ni tiquete que anular.");
                if (original.Status != InvoiceStatus.Accepted)
                    return OperationResult.Fail($"Solo se puede anular un comprobante aceptado por Hacienda; este está \"{original.Status.Humanize().ToLowerInvariant()}\". Actualizá el estado primero.");
                if (await _repository.GetActiveAsync(salesHeaderId, InvoiceDocType.NotaCredito) is { } existing)
                    return OperationResult.Fail($"La venta ya tiene una nota de crédito ({existing.Status.Humanize().ToLowerInvariant()}).");

                return await EmitCoreAsync(sale, InvoiceDocType.NotaCredito, original.PaymentMethod, original, dto.Reason.Trim(), user);
            }
            finally { EmitGate.Release(); }
        }

        // Guarda el intento, envía UNA vez y registra lo que pasó. Quien llama ya tiene el candado de emisión.
        private async Task<OperationResult> EmitCoreAsync(SalesHeader sale, InvoiceDocType docType, int paymentMethod, Invoice? reference, string? reason, string user)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            var securityCode = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
            var input = await NewInputAsync(sale, docType, paymentMethod, reference, reason, securityCode);
            var build = InvoiceJsonBuilder.Build(input, _options.CurrentValue);
            if (!build.Ok) return OperationResult.Fail(string.Join(" ", build.Errors));

            var invoice = new Invoice
            {
                SalesHeaderID = sale.SalesHeaderID,
                DocumentType = docType,
                Status = InvoiceStatus.Emitting,
                PaymentMethod = paymentMethod,
                ReferenceInvoiceID = reference?.InvoiceID,
                SecurityCode = securityCode,
                IssuedOn = nowUtc,
                RequestJson = build.Json!,
                Reason = reason,
                CreatedBy = user,
                CreatedOn = nowUtc
            };

            // 1) El rastro primero: si algo se cae después de enviar, esta fila "Enviando" dice que hay que conciliar.
            _repository.Add(invoice);
            try { await _repository.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                _repository.DiscardChanges();
                return OperationResult.Fail("La venta ya tiene un comprobante vigente de este tipo.");
            }

            // 2) Un solo envío. CancellationToken.None: si el navegador se cierra no se corta a la mitad.
            var transport = await _transport.PostAsync(invoice.RequestJson, CancellationToken.None);
            var reply = InvoiceResponse.ParseEmission(transport);

            // 3) Registrar el resultado.
            try
            {
                invoice.Status = reply.Status;
                invoice.SentOn = _time.GetUtcNow().UtcDateTime;
                invoice.ResponseText = Trim(transport.Body, MaxStored);
                invoice.HaciendaMessage = Trim(reply.Message, 1000);
                if (reply.Clave is not null)
                {
                    invoice.Clave = reply.Clave;
                    invoice.Consecutive = reply.Clave.Substring(21, 20); // el manual: clave[21..41]
                    invoice.XmlBase64 = reply.XmlBase64;
                }
                invoice.ModifiedBy = user;
                invoice.ModifiedOn = invoice.SentOn;
                await _repository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // El proveedor ya pudo haber emitido. La fila queda "Enviando" y esto deja todo lo necesario para conciliar.
                _repository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.Error, ModuleName, InvoiceTable, invoice.InvoiceID.ToString(),
                    $"No se pudo guardar el resultado de la emisión. Clave: {reply.Clave ?? "(sin clave)"}. Conciliar con el proveedor.",
                    postData: new { reply.Status, reply.Clave, Response = Trim(transport.Body, 2000) }, stackTrace: ex.ToString());
                return OperationResult.Fail("El proveedor respondió pero no se pudo guardar el resultado. NO vuelvas a emitir: revisá el comprobante con el proveedor.");
            }

            var failed = reply.Status is InvoiceStatus.Failed or InvoiceStatus.Uncertain;
            await _eventlogServices.LogAsync(failed ? typeEvent.AddFail : typeEvent.Add, ModuleName, InvoiceTable, invoice.InvoiceID.ToString(),
                $"{docType.Humanize()} de la venta {sale.SaleNumber}: {reply.Status.Humanize().ToLowerInvariant()}. {reply.Message}".Trim(),
                postData: new { invoice.InvoiceID, invoice.SalesHeaderID, invoice.DocumentType, invoice.Status, invoice.Clave, invoice.Consecutive });

            return reply.Status switch
            {
                InvoiceStatus.Sent => OperationResult.Ok(invoice.InvoiceID),
                InvoiceStatus.Failed => OperationResult.Fail($"No se emitió: {reply.Message}"),
                _ => OperationResult.Fail($"{reply.Message} El intento quedó \"por conciliar\": no se vuelve a enviar solo.")
            };
        }

        // ───────── Seguimiento ─────────

        public async Task<OperationResult> RefreshAsync(int invoiceId, string user)
        {
            var options = _options.CurrentValue;
            var guard = ConfigError();
            if (guard is not null) return OperationResult.Fail(guard);

            var invoice = await _repository.GetForUpdateAsync(invoiceId);
            if (invoice is null) return OperationResult.Fail(NotFoundMessage);
            if (string.IsNullOrEmpty(invoice.Clave))
                return OperationResult.Fail("Este intento no tiene clave: no hay nada que consultar.");

            var transport = await _transport.PostAsync(InvoiceJsonBuilder.BuildStatusQuery(invoice.Clave, options), CancellationToken.None);
            var reply = InvoiceResponse.ParseStatus(transport);
            if (!reply.Understood)
                return OperationResult.Fail(reply.Message ?? "No se pudo consultar el estado.");

            try
            {
                var before = invoice.Status;
                invoice.HaciendaStatus = Trim(reply.HaciendaStatus, 100);
                invoice.HaciendaMessage = Trim(reply.Message, 1000);
                invoice.PdfUrl = Trim(reply.PdfUrl, 500) ?? invoice.PdfUrl;
                invoice.PdfUrlTicket = Trim(reply.PdfUrlTicket, 500) ?? invoice.PdfUrlTicket;
                // Un comprobante ya aceptado no vuelve atrás; uno enviado o por conciliar con clave se resuelve con la respuesta final.
                if (invoice.Status is InvoiceStatus.Sent or InvoiceStatus.Uncertain && InvoiceResponse.FinalStatus(reply.HaciendaStatus) is { } final)
                    invoice.Status = final;
                invoice.ModifiedBy = user;
                invoice.ModifiedOn = _time.GetUtcNow().UtcDateTime;
                await _repository.SaveChangesAsync();

                if (before != invoice.Status)
                    await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, InvoiceTable, invoice.InvoiceID.ToString(),
                        $"Estado del comprobante: {before.Humanize().ToLowerInvariant()} → {invoice.Status.Humanize().ToLowerInvariant()}.",
                        preData: new { Status = before }, postData: new { invoice.Status, invoice.HaciendaStatus });
                return OperationResult.Ok(invoice.InvoiceID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _repository.DiscardChanges();
                return OperationResult.Fail("El comprobante cambió mientras se consultaba. Probá de nuevo.");
            }
        }

        public async Task<OperationResult> DiscardAsync(int invoiceId, DiscardInvoiceDTO dto, string user)
        {
            var invoice = await _repository.GetForUpdateAsync(invoiceId);
            if (invoice is null) return OperationResult.Fail(NotFoundMessage);

            var stuck = invoice.Status == InvoiceStatus.Emitting && _time.GetUtcNow().UtcDateTime - invoice.CreatedOn > StuckAfter;
            if (invoice.Status != InvoiceStatus.Uncertain && !stuck)
                return OperationResult.Fail("Solo se descartan intentos \"por conciliar\" o trabados en \"enviando\".");

            try
            {
                var before = invoice.Status;
                invoice.Status = InvoiceStatus.Failed;
                invoice.Reason = Trim($"Descartado: {dto.Reason.Trim()}", 250);
                invoice.ModifiedBy = user;
                invoice.ModifiedOn = _time.GetUtcNow().UtcDateTime;
                await _repository.SaveChangesAsync();
                await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, InvoiceTable, invoice.InvoiceID.ToString(),
                    $"Intento descartado por el administrador: {dto.Reason.Trim()}",
                    preData: new { Status = before, invoice.Clave }, postData: new { invoice.Status });
                return OperationResult.Ok(invoice.InvoiceID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _repository.DiscardChanges();
                return OperationResult.Fail("El comprobante cambió mientras se descartaba. Recargá e intentá de nuevo.");
            }
        }

        public async Task<OperationResult> SendEmailAsync(int invoiceId, string user)
        {
            var invoice = await _repository.GetAsync(invoiceId);
            if (invoice is null) return OperationResult.Fail(NotFoundMessage);
            if (invoice.Status != InvoiceStatus.Accepted)
                return OperationResult.Fail("Solo se envían por correo los comprobantes aceptados por Hacienda.");
            if (string.IsNullOrWhiteSpace(invoice.PdfUrl))
                return OperationResult.Fail("Todavía no hay enlace al PDF. Actualizá el estado primero.");
            if (!_emailServices.Enabled)
                return OperationResult.Fail("El envío de correos está deshabilitado (Email:Enabled).");

            var sale = await _repository.GetSaleForInvoiceAsync(invoice.SalesHeaderID);
            if (sale is null) return OperationResult.Fail(NotFoundMessage);
            if (string.IsNullOrWhiteSpace(sale.ClientEmail)) return OperationResult.Fail("El cliente no tiene correo registrado.");

            // La nota de crédito explica qué factura anula; la factura y el tiquete usan el texto general.
            string body;
            if (invoice.DocumentType == InvoiceDocType.NotaCredito)
            {
                var original = invoice.ReferenceInvoiceID is int refId ? await _repository.GetAsync(refId) : null;
                var originalNumber = original?.Consecutive is { Length: 20 } c ? c[10..] : null;
                body = EmailTemplates.CreditNoteIssued(sale.ClientName, sale.SaleNumber, originalNumber, sale.TotalAmount, invoice.Reason, invoice.PdfUrl, invoice.Clave ?? string.Empty);
            }
            else
                body = EmailTemplates.InvoiceIssued(sale.ClientName, invoice.DocumentType.Humanize(), sale.SaleNumber, sale.TotalAmount, invoice.PdfUrl, invoice.Clave ?? string.Empty);

            await _emailServices.QueueAndSaveAsync(new EmailMessage(
                sale.ClientEmail, sale.ClientName,
                $"{invoice.DocumentType.Humanize()} {invoice.Consecutive?[10..] ?? string.Empty} - SAM DESIGN".Replace("  ", " "),
                body,
                EmailKinds.Invoice, InvoiceTable, invoice.InvoiceID.ToString(),
                // El enlace al PDF es personal: se borra de la cola apenas se envía.
                IsSensitive: true), user);

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, InvoiceTable, invoice.InvoiceID.ToString(),
                $"Comprobante enviado por correo a {sale.ClientEmail}.");
            return OperationResult.Ok(invoice.InvoiceID);
        }

        public async Task<(string FileName, byte[] Bytes)?> GetXmlAsync(int invoiceId)
        {
            var invoice = await _repository.GetAsync(invoiceId);
            if (invoice?.XmlBase64 is null || invoice.Clave is null) return null;
            try { return ($"{invoice.Clave}.xml", Convert.FromBase64String(invoice.XmlBase64)); }
            catch (FormatException) { return null; }
        }

        // ───────── Ayudas ─────────

        private string? ConfigError()
        {
            var options = _options.CurrentValue;
            if (!options.Enabled) return "La facturación electrónica está deshabilitada (Invoicing:Enabled).";
            var missing = options.MissingSettings();
            return missing.Count > 0 ? $"Falta configurar el emisor: {string.Join(", ", missing)}." : null;
        }

        // Solo se emite factura electrónica (ventas con IVA). Las ventas sin IVA llevan un tiquete de caja no fiscal que no pasa por acá.
        private static InvoiceDocType DocTypeFor(SalesHeader sale) => InvoiceDocType.Factura;

        // El medio de pago del primer abono vigente (por fecha); si no hay abonos, el que elija quien factura.
        private static int? ResolvePaymentMethod(SalesHeader sale, int? chosen)
        {
            var first = sale.Payments.Where(p => !p.IsVoided).OrderBy(p => p.PaymentDate).ThenBy(p => p.SalesPaymentID).FirstOrDefault();
            if (first is not null) return (int)first.PaymentMethod;
            return chosen is 1 or 2 or 3 or 4 or 6 or 7 or 99 ? chosen : null;
        }

        private async Task<InvoiceBuildInput> NewInputAsync(SalesHeader sale, InvoiceDocType docType, int paymentMethod, Invoice? reference, string? reason, string securityCode)
        {
            var activity = await _repository.GetClientActivityAsync(sale.ClientID);
            var localNow = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).DateTime;
            var ivaPercent = Math.Round((_pricing.Value.IvaRate - 1m) * 100m, 2);
            return new InvoiceBuildInput(sale, activity, docType, paymentMethod, localNow, securityCode, ivaPercent, reference, reason);
        }

        // La vista previa tiene que funcionar aunque el emisor todavía no esté configurado.
        private static InvoicingOptions WithDummyProfile(InvoicingOptions o) => new()
        {
            Enabled = o.Enabled, Endpoint = o.Endpoint, ProfileId = 0, IssuerId = o.IssuerId, ActivityCode = o.ActivityCode,
            BranchCode = o.BranchCode, TerminalCode = o.TerminalCode, RegisterUser = o.RegisterUser, SituationCode = o.SituationCode,
            TimeoutSeconds = o.TimeoutSeconds, UnitCodes = o.UnitCodes
        };

        private static bool CanAccess(SalesHeader sale, string user, bool isAdmin) =>
            isAdmin || string.Equals(sale.Seller, user, StringComparison.OrdinalIgnoreCase);

        private static string? Trim(string? text, int max) =>
            text is null ? null : text.Length <= max ? text : text[..max];

        private static InvoiceDTO Map(Invoice i) => new()
        {
            Id = i.InvoiceID,
            SalesHeaderID = i.SalesHeaderID,
            DocumentType = (int)i.DocumentType,
            DocumentTypeName = i.DocumentType.Humanize(),
            Status = (int)i.Status,
            StatusName = i.Status.Humanize(),
            Clave = i.Clave,
            Consecutive = i.Consecutive,
            PaymentMethod = i.PaymentMethod,
            ReferenceInvoiceID = i.ReferenceInvoiceID,
            IssuedOn = i.IssuedOn,
            HaciendaStatus = i.HaciendaStatus,
            HaciendaMessage = i.HaciendaMessage,
            PdfUrl = i.PdfUrl,
            PdfUrlTicket = i.PdfUrlTicket,
            Reason = i.Reason,
            HasXml = !string.IsNullOrEmpty(i.XmlBase64),
            CreatedBy = i.CreatedBy,
            CreatedOn = i.CreatedOn
        };
    }
}
