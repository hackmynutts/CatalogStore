using CatalogStore.UI.Models.Inventory;
using CatalogStore.UI.Models.InventoryLine;
using CatalogStore.UI.Models.InventoryTransaction;
using CatalogStore.UI.Models.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    // Líneas de inventario (qué productos tiene cada bodega). Todo se muestra dentro del detalle de la bodega:
    // la tabla llega como parcial por AJAX y los formularios en un modal, sin recargar la página.
    [Authorize(Roles = "Admin,AdminIT")]
    public class InventoryLineController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public InventoryLineController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: InventoryLine/ByInventory/5 — tabla de líneas de una bodega (parcial)
        [HttpGet]
        public async Task<IActionResult> ByInventory(int id)
        {
            ViewData["InventoryId"] = id;
            return PartialView("_LinesPartial", await GetLinesAsync(id));
        }

        // GET: InventoryLine/Details/5 — detalle de un producto dentro de una bodega, con su historial de movimientos
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/InventoryLine/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var line = await response.Content.ReadFromJsonAsync<InventoryLineViewModel>();
            if (line == null) return NotFound();

            // El nombre de la bodega es solo para el encabezado; si falla, la página se muestra igual.
            var inventoryResponse = await client.GetAsync($"api/Inventory/{line.InventoryID}");
            var inventory = inventoryResponse.IsSuccessStatusCode
                ? await inventoryResponse.Content.ReadFromJsonAsync<InventoryViewModel>()
                : null;
            ViewData["InventoryName"] = inventory?.Name ?? "Bodega";

            // Historial de movimientos. Si falla se distingue de "sin movimientos" para no confundir al usuario.
            var transactionsResponse = await client.GetAsync($"api/InventoryTransaction/line/{id}");
            ViewData["TransactionsLoaded"] = transactionsResponse.IsSuccessStatusCode;
            ViewData["Transactions"] = transactionsResponse.IsSuccessStatusCode
                ? await transactionsResponse.Content.ReadFromJsonAsync<List<InventoryTransactionViewModel>>() ?? new List<InventoryTransactionViewModel>()
                : new List<InventoryTransactionViewModel>();

            return View(line);
        }

        // GET: InventoryLine/CreatePartial/5 — formulario de alta; solo ofrece productos activos que aún no tienen línea en la bodega
        [HttpGet]
        public async Task<IActionResult> CreatePartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Product/active");
            var products = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<ProductViewModel>>() ?? new List<ProductViewModel>()
                : new List<ProductViewModel>();

            var assigned = (await GetLinesAsync(id)).Select(l => l.ProductID).ToHashSet();

            ViewData["ProductOptions"] = products
                .Where(p => !assigned.Contains(p.ProductID))
                .OrderBy(p => p.ProductName)
                .ToList();
            ViewData["ProductsLoaded"] = response.IsSuccessStatusCode;

            return PartialView("_CreateLinePartial", new AddInventoryLineViewModel { InventoryID = id });
        }

        // POST: InventoryLine/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddInventoryLineViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/InventoryLine", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo agregar el producto a la bodega.", details = errorBody });
            }

            return Json(new { success = true });
        }

        // GET: InventoryLine/EditPartial/5 — mínimo de reposición y estado (el stock no se edita aquí)
        [HttpGet]
        public async Task<IActionResult> EditPartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/InventoryLine/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var line = await response.Content.ReadFromJsonAsync<InventoryLineViewModel>();
            if (line == null) return NotFound();

            ViewData["Line"] = line;
            return PartialView("_EditLinePartial", new UpdateInventoryLineViewModel
            {
                InventoryLineID = line.InventoryLineID,
                QuantityRestock = line.QuantityRestock,
                StatusID = line.StatusID
            });
        }

        // POST: InventoryLine/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateInventoryLineViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/InventoryLine/{id}", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo actualizar la línea.", details = errorBody });
            }

            return Json(new { success = true });
        }

        // POST: InventoryLine/Inactivate/5 — la API rechaza si la línea tiene existencias o reservas
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inactivate(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsync($"api/InventoryLine/{id}/inactivate", null);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo inactivar la línea.", details = errorBody });
            }

            return Json(new { success = true });
        }

        private async Task<List<InventoryLineViewModel>> GetLinesAsync(int inventoryId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/InventoryLine/inventory/{inventoryId}");
            if (!response.IsSuccessStatusCode)
                return new List<InventoryLineViewModel>();

            return await response.Content.ReadFromJsonAsync<List<InventoryLineViewModel>>() ?? new List<InventoryLineViewModel>();
        }
    }
}
