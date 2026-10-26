using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            ViewData["JustCreated"] = created;
            return View(sale);
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
    }
}
