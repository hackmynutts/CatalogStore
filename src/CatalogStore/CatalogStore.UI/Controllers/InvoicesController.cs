using CatalogStore.UI.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CatalogStore.UI.Controllers
{
    // Acciones de facturación electrónica de una venta. Solo administradores; llaman a api/Invoice.
    // Las acciones POST devuelven JSON { success, message } para sales-invoice.js.
    [Authorize(Roles = "Admin,AdminIT")]
    public class InvoicesController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public InvoicesController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        // GET: Invoices/Preview?saleId=5&paymentMethod=1 — revisa todo sin enviar nada; el JSON de la API se reenvía tal cual
        public async Task<IActionResult> Preview(int saleId, int? paymentMethod)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var url = $"api/Invoice/sale/{saleId}/preview" + (paymentMethod.HasValue ? $"?paymentMethod={paymentMethod.Value}" : string.Empty);
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo revisar la venta.") });
            return Json(new { success = true, preview = await response.Content.ReadFromJsonAsync<JsonElement>() });
        }

        // POST: Invoices/Emit — emite la factura (con IVA) o el tiquete (sin IVA). ¡Es un comprobante real!
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Emit(int salesHeaderId, int? paymentMethod) =>
            Send($"api/Invoice/sale/{salesHeaderId}/emit", new { paymentMethod }, "No se pudo emitir el comprobante.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> CreditNote(int salesHeaderId, string? reason) =>
            Send($"api/Invoice/sale/{salesHeaderId}/credit-note", new { reason }, "No se pudo emitir la nota de crédito.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Refresh(int id) => Send($"api/Invoice/{id}/refresh", null, "No se pudo consultar el estado.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Discard(int id, string? reason) =>
            Send($"api/Invoice/{id}/discard", new { reason }, "No se pudo descartar el intento.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Email(int id) => Send($"api/Invoice/{id}/email", null, "No se pudo enviar el correo.");

        // GET: Invoices/Xml/9 — descarga el XML firmado
        public async Task<IActionResult> Xml(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Invoice/{id}/xml");
            if (!response.IsSuccessStatusCode) return NotFound();
            var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? $"comprobante-{id}.xml";
            return File(await response.Content.ReadAsByteArrayAsync(), "application/xml", name);
        }

        private async Task<IActionResult> Send(string url, object? body, string fallback)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            // CancellationToken.None: si el navegador se cierra a medias no se corta la llamada (el backend ya estaría emitiendo).
            var response = body is null
                ? await client.PostAsync(url, null, CancellationToken.None)
                : await client.PostAsJsonAsync(url, body, CancellationToken.None);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, fallback) });
        }
    }
}
