using CatalogStore.UI.Models.Client;
using CatalogStore.UI.Models.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    [Authorize]
    public class ClientController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public ClientController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        // GET: ClientController — solo clientes activos
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Client/active");

            if (!response.IsSuccessStatusCode)
                return View(new List<ClientViewModel>());

            var clientes = await response.Content.ReadFromJsonAsync<List<ClientViewModel>>();
            return View(clientes ?? new List<ClientViewModel>());
        }

        // GET: ClientController/IndexAdmin — todos, incluidos inactivos
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> IndexAdmin()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Client");

            if (!response.IsSuccessStatusCode)
                return View("Index", new List<ClientViewModel>());

            var clientes = await response.Content.ReadFromJsonAsync<List<ClientViewModel>>();
            return View("Index", clientes ?? new List<ClientViewModel>());
        }

        // GET: ClientController/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Client/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var cliente = await response.Content.ReadFromJsonAsync<ClientViewModel>();
            if (cliente == null) return NotFound();
            return View(cliente);
        }

        // GET: ClientController/Lookup?identification=117001454419 — proxy a Hacienda vía la API
        [HttpGet]
        public async Task<IActionResult> Lookup(string identification)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Client/lookup?identification={Uri.EscapeDataString(identification)}");

            if (!response.IsSuccessStatusCode)
                return Json(new { nombre = (string?)null });

            var result = await response.Content.ReadFromJsonAsync<HaciendaLookupResult>();
            return Json(new { nombre = result?.Nombre });
        }

        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public IActionResult CreatePartial()
        {
            return PartialView("_CreateClientPartial", new AddClientViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddClientViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/Client", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo crear el cliente.", details = errorBody });
            }

            return Json(new { success = true });
        }

        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> EditPartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Client/{id}");

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var cliente = await response.Content.ReadFromJsonAsync<ClientViewModel>();
            if (cliente == null) return NotFound();

            var model = new UpdateClientViewModel
            {
                ClientName = cliente.ClientName,
                ClientPhone = cliente.ClientPhone,
                ClientEmail = cliente.ClientEmail,
                ClientAddress = cliente.ClientAddress,
                StatusID = cliente.StatusID
            };

            var statusResponse = await client.GetAsync("api/Status");
            var statusOptions = statusResponse.IsSuccessStatusCode
                ? await statusResponse.Content.ReadFromJsonAsync<List<StatusViewModel>>()
                : new List<StatusViewModel>();

            ViewData["ClientId"] = id;
            ViewData["StatusOptions"] = statusOptions ?? new List<StatusViewModel>();
            return PartialView("_EditClientPartial", model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateClientViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Client/{id}/update", model);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo actualizar el cliente.", details = errorBody });
            }

            return Json(new { success = true });
        }

        // POST: ClientController/Inactivate/5 — sin form propio: trae el cliente actual y lo reenvía con StatusID inactivo.
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inactivate(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var getResponse = await client.GetAsync($"api/Client/{id}");
            if (!getResponse.IsSuccessStatusCode)
                return Json(new { success = false, message = "Cliente no encontrado." });

            var cliente = await getResponse.Content.ReadFromJsonAsync<ClientViewModel>();
            if (cliente == null)
                return Json(new { success = false, message = "Cliente no encontrado." });

            var model = new UpdateClientViewModel
            {
                ClientName = cliente.ClientName,
                ClientPhone = cliente.ClientPhone,
                ClientEmail = cliente.ClientEmail,
                ClientAddress = cliente.ClientAddress,
                StatusID = cliente.StatusID
            };

            var response = await client.PutAsJsonAsync($"api/Client/{id}/inactivate", model);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "No se pudo inactivar el cliente." });

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            await client.DeleteAsync($"api/Client/{id}");

            return RedirectToAction(nameof(Index));
        }
    }
}
