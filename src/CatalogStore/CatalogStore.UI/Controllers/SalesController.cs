using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Invoicing;
using CatalogStore.UI.Models.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using System.Globalization;

namespace CatalogStore.UI.Controllers
{
    // Páginas de ventas (listado y detalle) y acciones de cobro, que llaman a api/Sale.
    // Las acciones POST devuelven JSON { success, message } para sales-details.js.
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class SalesController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public SalesController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        // GET: Sales?status=0&clientId=5&overdue=true — el vendedor recibe solo sus ventas (lo filtra la API).
        public async Task<IActionResult> Index(PaymentStatus? status, int? clientId, bool overdue = false)
        {
            var query = new List<string>();
            if (status.HasValue) query.Add($"status={(int)status.Value}");
            if (clientId.HasValue) query.Add($"clientId={clientId.Value}");
            if (overdue) query.Add("overdue=true");
            var url = "api/Sale" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync(url);
            var sales = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<SaleListItemViewModel>>() ?? new()
                : new List<SaleListItemViewModel>();

            ViewData["Status"] = status;
            ViewData["ClientId"] = clientId;
            ViewData["Overdue"] = overdue;
            ViewData["ClientName"] = clientId.HasValue ? sales.FirstOrDefault()?.ClientName : null;
            return View(sales);
        }

        // GET: Sales/IndexAdmin — enlace heredado del menú IT; es la misma lista.
        public IActionResult IndexAdmin() => RedirectToAction(nameof(Index));

        // GET: Sales/Details/5?created=true (created: se llegó recién al completar la orden)
        public async Task<IActionResult> Details(int id, bool created = false)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Sale/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var sale = await response.Content.ReadFromJsonAsync<SaleViewModel>();
            if (sale is null)
                return NotFound();

            // Comprobantes electrónicos de la venta. Si la API de facturación falla la venta se muestra igual, sin esa tarjeta.
            var invoices = new List<InvoiceViewModel>();
            var invoiceResponse = await client.GetAsync($"api/Invoice/sale/{id}");
            if (invoiceResponse.IsSuccessStatusCode)
                invoices = await invoiceResponse.Content.ReadFromJsonAsync<List<InvoiceViewModel>>() ?? new();
            ViewData["Invoices"] = invoices;

            ViewData["JustCreated"] = created;
            return View(sale);
        }

        // GET: Sales/Receipt/5 — comprobante de venta (NO fiscal) de una venta sin IVA, con el formato de la factura: se imprime y se descarga.
        // Las ventas con IVA llevan factura electrónica y las anuladas no se imprimen: ambas vuelven al detalle.
        public async Task<IActionResult> Receipt(int id)
        {
            var sale = await LoadReceiptSaleAsync(id);
            if (sale is null) return NotFound();
            if (sale.ApplyIva || sale.IsVoided)
                return RedirectToAction(nameof(Details), new { id });
            return View(sale);
        }

        // GET: Sales/ReceiptPdf/5 — el mismo comprobante como archivo PDF.
        public async Task<IActionResult> ReceiptPdf(int id, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
        {
            var sale = await LoadReceiptSaleAsync(id);
            if (sale is null) return NotFound();
            if (sale.ApplyIva || sale.IsVoided)
                return RedirectToAction(nameof(Details), new { id });

            var logoPath = env.WebRootPath is null ? null : Path.Combine(env.WebRootPath, "img", "logos", "logo_pequeño-removebg-preview.png");
            var contact = string.Join(" · ", new[] { config["Landing:WhatsApp"], config["Landing:Email"] }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var saleLocal = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.SpecifyKind(sale.SaleDate, DateTimeKind.Utc), "America/Costa_Rica");
            var document = new Documents.SaleReceiptPdfDocument(sale, saleLocal, contact,
                logoPath is not null && System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null);
            return File(document.GeneratePdf(), "application/pdf", $"Comprobante-{sale.SaleNumber}.pdf");
        }

        private async Task<SaleViewModel?> LoadReceiptSaleAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Sale/{id}");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SaleViewModel>() : null;
        }

        // GET: Sales/Invoice/5 — la factura electrónica ACEPTADA de la venta, para imprimirla con el formato de la casa.
        public async Task<IActionResult> Invoice(int id)
        {
            var found = await LoadAcceptedInvoiceAsync(id);
            if (found is null) return RedirectToAction(nameof(Details), new { id });
            ViewData["Invoice"] = found.Value.Invoice;
            return View(found.Value.Sale);
        }

        // GET: Sales/InvoicePdf/5 — la misma factura como archivo PDF.
        // Solo la factura electrónica (ventas con IVA); las ventas sin IVA tienen su propio comprobante (ReceiptPdf).
        public async Task<IActionResult> InvoicePdf(int id, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
        {
            var found = await LoadAcceptedInvoiceAsync(id);
            if (found is null) return RedirectToAction(nameof(Details), new { id });
            var (sale, invoice) = found.Value;

            var logoPath = env.WebRootPath is null ? null : Path.Combine(env.WebRootPath, "img", "logos", "logo_pequeño-removebg-preview.png");
            var contact = string.Join(" · ", new[] { config["Landing:WhatsApp"], config["Landing:Email"] }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var document = new Documents.InvoicePdfDocument(sale, invoice, InvoiceLocalTime(invoice), contact,
                logoPath is not null && System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null);
            return File(document.GeneratePdf(), "application/pdf", $"Factura-{invoice.Number}.pdf");
        }

        // GET: Sales/CreditNote/5 — la nota de crédito ACEPTADA de la venta (la que anuló su factura), para imprimirla.
        public async Task<IActionResult> CreditNote(int id)
        {
            var found = await LoadAcceptedCreditNoteAsync(id);
            if (found is null) return RedirectToAction(nameof(Details), new { id });
            ViewData["Invoice"] = found.Value.CreditNote;
            ViewData["Reference"] = found.Value.Original;
            return View("Invoice", found.Value.Sale);
        }

        // GET: Sales/CreditNotePdf/5 — la misma nota de crédito como archivo PDF.
        public async Task<IActionResult> CreditNotePdf(int id, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
        {
            var found = await LoadAcceptedCreditNoteAsync(id);
            if (found is null) return RedirectToAction(nameof(Details), new { id });
            var (sale, creditNote, original) = found.Value;

            var logoPath = env.WebRootPath is null ? null : Path.Combine(env.WebRootPath, "img", "logos", "logo_pequeño-removebg-preview.png");
            var contact = string.Join(" · ", new[] { config["Landing:WhatsApp"], config["Landing:Email"] }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var document = new Documents.InvoicePdfDocument(sale, creditNote, InvoiceLocalTime(creditNote), contact,
                logoPath is not null && System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null, original);
            return File(document.GeneratePdf(), "application/pdf", $"Nota-credito-{creditNote.Number}.pdf");
        }

        // Venta con IVA y una nota de crédito aceptada. La venta puede estar anulada (es justo lo que la nota habilita).
        private async Task<(SaleViewModel Sale, InvoiceViewModel CreditNote, InvoiceViewModel? Original)?> LoadAcceptedCreditNoteAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Sale/{id}");
            if (!response.IsSuccessStatusCode) return null;
            var sale = await response.Content.ReadFromJsonAsync<SaleViewModel>();
            if (sale is null || !sale.ApplyIva) return null;

            var invoiceResponse = await client.GetAsync($"api/Invoice/sale/{id}");
            if (!invoiceResponse.IsSuccessStatusCode) return null;
            var invoices = await invoiceResponse.Content.ReadFromJsonAsync<List<InvoiceViewModel>>() ?? new();
            var creditNote = invoices.Where(i => i.IsCreditNote && i.Status == InvoiceUi.Accepted).OrderByDescending(i => i.Id).FirstOrDefault();
            if (creditNote is null) return null;
            return (sale, creditNote, invoices.FirstOrDefault(i => i.Id == creditNote.ReferenceInvoiceID));
        }

        public static DateTime InvoiceLocalTime(InvoiceViewModel invoice) =>
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.SpecifyKind(invoice.IssuedOn, DateTimeKind.Utc), "America/Costa_Rica");

        // Venta con IVA, no anulada, con una factura aceptada y sin nota de crédito vigente. Cualquier otro caso: null (vuelve al detalle).
        private async Task<(SaleViewModel Sale, InvoiceViewModel Invoice)?> LoadAcceptedInvoiceAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Sale/{id}");
            if (!response.IsSuccessStatusCode) return null;
            var sale = await response.Content.ReadFromJsonAsync<SaleViewModel>();
            if (sale is null || !sale.ApplyIva || sale.IsVoided) return null;

            var invoiceResponse = await client.GetAsync($"api/Invoice/sale/{id}");
            if (!invoiceResponse.IsSuccessStatusCode) return null;
            var invoices = await invoiceResponse.Content.ReadFromJsonAsync<List<InvoiceViewModel>>() ?? new();
            var invoice = invoices.Where(i => !i.IsCreditNote && i.Status == InvoiceUi.Accepted).OrderByDescending(i => i.Id).FirstOrDefault();
            if (invoice is null || invoices.Any(i => i.IsCreditNote && i.ReferenceInvoiceID == invoice.Id && InvoiceUi.IsActive(i.Status))) return null;
            return (sale, invoice);
        }

        // POST: Sales/RegisterPayment — el monto llega como texto invariante ("1500.50") para no depender de la cultura.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPayment(int salesHeaderId, string? amount, int? paymentMethod, string? paymentDate, string? reference, string? notes)
        {
            if (!decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amountValue) || amountValue <= 0)
                return Json(new { success = false, message = "Ingresá un monto mayor a cero." });

            DateOnly? date = null;
            if (!string.IsNullOrWhiteSpace(paymentDate))
            {
                if (!DateOnly.TryParseExact(paymentDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return Json(new { success = false, message = "La fecha de pago no es válida." });
                date = parsed;
            }

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync($"api/Sale/{salesHeaderId}/payments",
                new { amount = amountValue, paymentMethod, paymentDate = date, reference, notes });
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo registrar el abono.") });
        }

        // POST: Sales/VoidPayment — solo admin (lo valida la API; la vista solo muestra el botón a admins).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidPayment(int salesHeaderId, int salesPaymentId, string? reason)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync($"api/Sale/{salesHeaderId}/payments/{salesPaymentId}/void", new { reason = reason ?? string.Empty });
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo anular el abono.") });
        }

        // POST: Sales/Exchange — cambio por producto defectuoso: unidades de una línea se cambian por las mismas. Solo admin (lo valida la API).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Exchange(int salesHeaderId, int salesLineId, int quantity, string? reason)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync($"api/Sale/{salesHeaderId}/exchange",
                new { salesLineID = salesLineId, quantity, reason = reason ?? string.Empty });
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo registrar el cambio.") });
        }

        // POST: Sales/VoidSale — anular la venta completa. Solo admin y sin abonos vigentes (lo valida la API).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidSale(int salesHeaderId, string? reason)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync($"api/Sale/{salesHeaderId}/void", new { reason = reason ?? string.Empty });
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo anular la venta.") });
        }
    }
}
