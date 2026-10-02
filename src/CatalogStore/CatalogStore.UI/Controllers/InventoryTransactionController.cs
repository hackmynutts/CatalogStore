using CatalogStore.UI.Models.InventoryLine;
using CatalogStore.UI.Models.InventoryTransaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CatalogStore.UI.Controllers
{
    // Movimientos manuales de stock desde el detalle de una línea. El historial se renderiza en
    // InventoryLine/Details; aquí solo vive el formulario (modal) y su envío.
    [Authorize(Roles = "Admin,AdminIT")]
    public class InventoryTransactionController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public InventoryTransactionController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: InventoryTransaction/CreatePartial/5 — formulario de movimiento para la línea 5
        [HttpGet]
        public async Task<IActionResult> CreatePartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/InventoryLine/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var line = await response.Content.ReadFromJsonAsync<InventoryLineViewModel>();
            if (line == null) return NotFound();

            // Stock, reservado y disponible actuales, para que el usuario sepa cuánto puede ajustar.
            ViewData["Line"] = line;
            return PartialView("_CreateTransactionPartial", new RegisterInventoryTransactionViewModel { InventoryLineID = id });
        }

        // POST: InventoryTransaction/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterInventoryTransactionViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/InventoryTransaction/register", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo registrar el movimiento.", details = errorBody });
            }

            return Json(new { success = true });
        }

        // POST: InventoryTransaction/ImportStock/1?dryRun=true — carga inicial de stock del proveedor en la bodega 1.
        // Con dryRun solo simula. El resumen del backend se reenvía tal cual al JS.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportStock(int id, bool dryRun = true)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/InventoryTransaction/import-stock/{id}?dryRun={(dryRun ? "true" : "false")}", null);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo cargar el stock del proveedor.", details = errorBody });
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return Json(new { success = true, result });
        }
    }
}
