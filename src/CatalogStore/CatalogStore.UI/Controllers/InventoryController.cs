using CatalogStore.UI.Models.Inventory;
using CatalogStore.UI.Models.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public InventoryController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Inventory/active");

            if ( !response.IsSuccessStatusCode)           
                return View(new List<InventoryViewModel>());

            var inventoryList = await response.Content.ReadFromJsonAsync<List<InventoryViewModel>>();
            return View(inventoryList ?? new List<InventoryViewModel>());
        }
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> IndexAdmin()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Inventory");

            if ( !response.IsSuccessStatusCode)
                return View("Index", new List<InventoryViewModel>());

            var inventoryList = await response.Content.ReadFromJsonAsync<List<InventoryViewModel>>();
            return View("Index", inventoryList ?? new List<InventoryViewModel>());
        }

        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Details(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Inventory/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var inventory = await response.Content.ReadFromJsonAsync<InventoryViewModel>();
            if (inventory == null) return NotFound();
            return View(inventory);
        }

        [Authorize(Roles = "Admin,AdminIT")]
        public IActionResult CreatePartial()
        {
            return PartialView("_CreateInventoryPartial", new AddInventoryViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddInventoryViewModel model)
        {
            model.CreatedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/Inventory", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo crear el inventario.", details = errorBody });
            }

            return Json(new { success = true });
        }

        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> EditPartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Inventory/{id}");

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var inventory = await response.Content.ReadFromJsonAsync<InventoryViewModel>();
            if (inventory == null) return NotFound();

            var model = new UpdateInventoryViewModel
            {
                InventoryID = inventory.InventoryID,
                Name = inventory.Name,
                Descripcion = inventory.Descripcion,
                StatusID = inventory.StatusID
            };

            var statusResponse = await client.GetAsync("api/Status");
            var statusOptions = statusResponse.IsSuccessStatusCode
                ? await statusResponse.Content.ReadFromJsonAsync<List<StatusViewModel>>()
                : new List<StatusViewModel>();

            ViewData["InventoryId"] = id;
            ViewData["StatusOptions"] = statusOptions ?? new List<StatusViewModel>();
            return PartialView("_EditInventoryPartial", model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateInventoryViewModel model)
        {
            model.ModifiedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Inventory/{id}", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo actualizar el inventario.", details = errorBody });
            }

            return Json(new { success = true });
        }

        // POST: InventoryController/Inactivate/5 — sin form propio: trae el inventario actual y lo reenvía con StatusID inactivo.
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inactivate(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var getResponse = await client.GetAsync($"api/Inventory/{id}");
            if (!getResponse.IsSuccessStatusCode)
                return Json(new { success = false, message = "Inventario no encontrado." });

            var inventory = await getResponse.Content.ReadFromJsonAsync<InventoryViewModel>();
            if (inventory == null)
                return Json(new { success = false, message = "Inventario no encontrado." });

            var model = new UpdateInventoryViewModel
            {
                InventoryID = inventory.InventoryID,
                Name = inventory.Name,
                Descripcion = inventory.Descripcion,
                StatusID = inventory.StatusID
            };

            var response = await client.PutAsJsonAsync($"api/Inventory/{id}/inactivate", model);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "No se pudo inactivar el inventario." });

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            await client.DeleteAsync($"api/Inventory/{id}");

            return RedirectToAction(nameof(Index));
        }
    }
}
