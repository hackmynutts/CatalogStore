using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Inventory;
using CatalogStore.UI.Models.InventoryLine;
using CatalogStore.UI.Models.InventoryTransaction;
using CatalogStore.UI.Models.Product;
using CatalogStore.UI.Models.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CatalogStore.UI.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public ProductController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private void ResolveImageUrls(IEnumerable<ProductViewModel> productos)
        {
            var baseUrl = _configuration["BackendApi:PublicBaseUrl"]!.TrimEnd('/');
            foreach (var producto in productos)
                foreach (var img in producto.Images)
                    img.Url = baseUrl + img.Url;
        }

        // GET: ProductController — solo productos activos
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Product/active");

            if (!response.IsSuccessStatusCode)
                return View(new List<ProductViewModel>());

            var productos = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>() ?? new List<ProductViewModel>();
            ResolveImageUrls(productos);
            return View(productos);
        }

        // GET: ProductController/Catalog — vista tipo catálogo para que el vendedor le muestre al cliente
        public async Task<IActionResult> Catalog()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Product/active");

            if (!response.IsSuccessStatusCode)
                return View(new List<ProductViewModel>());

            var productos = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>() ?? new List<ProductViewModel>();
            // Variantes de un mismo producto seguidas: categoría → modelo → color → talla (S, M, L, XL).
            productos = CatalogOrder.Sort(productos, p => p.categoria, p => p.ProductName);
            ResolveImageUrls(productos);
            // Unidades disponibles por producto. Si falla (o el rol no tiene permiso) las tarjetas simplemente no muestran stock.
            ViewData["Availability"] = await LoadAvailabilityAsync(client);
            return View(productos);
        }

        // GET: Product/Availability — { productId: disponibles }; el catálogo lo refresca después de agregar a la orden
        public async Task<IActionResult> Availability()
        {
            var availability = await LoadAvailabilityAsync(_httpClientFactory.CreateClient("BackendApi"));
            return availability is null ? StatusCode(502) : Json(availability);
        }

        private static async Task<Dictionary<int, int>?> LoadAvailabilityAsync(HttpClient client)
        {
            var response = await client.GetAsync("api/Product/availability");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Dictionary<int, int>>() : null;
        }

        // GET: ProductController/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Product/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var producto = await response.Content.ReadFromJsonAsync<ProductViewModel>();
            if (producto == null) return NotFound();
            ResolveImageUrls(new[] { producto });
            return View(producto);
        }
        // GET: ProductController/Details/5
        public async Task<IActionResult> DetailsByCode(string code)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Product/code/{Uri.EscapeDataString(code)}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var producto = await response.Content.ReadFromJsonAsync<ProductViewModel>();
            if (producto == null) return NotFound();
            ResolveImageUrls(new[] { producto });
            return RedirectToAction(nameof(Details), new { id = producto.ProductID });
        }

        // GET: ProductController/IndexAdmin — todos, incluidos inactivos
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> IndexAdmin()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Product");

            if (!response.IsSuccessStatusCode)
                return View("Index", new List<ProductViewModel>());

            var productos = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>();
            return View("Index", productos ?? new List<ProductViewModel>());
        }

        [Authorize(Roles = "Admin,AdminIT")]
        public IActionResult CreatePartial()
        {
            return PartialView("_CreateProductPartial", new AddProductViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AddProductViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/Product", model);

            if (!response.IsSuccessStatusCode)
                return await ErrorJsonAsync(response, "No se pudo crear el producto.");

            // El id sirve para seguir con el paso 2 (agregarlo al inventario) sin recargar la página.
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            int? id = created.ValueKind == JsonValueKind.Object && created.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var value) ? value : null;
            return Json(new { success = true, id });
        }

        // GET: Product/InventoryStepPartial/5 — paso 2 del alta: ¿se agrega el producto recién creado al inventario?
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> InventoryStepPartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var productResponse = await client.GetAsync($"api/Product/{id}");
            if (!productResponse.IsSuccessStatusCode)
                return NotFound();
            var product = await productResponse.Content.ReadFromJsonAsync<ProductViewModel>();
            if (product == null) return NotFound();

            var inventoryResponse = await client.GetAsync("api/Inventory/active");
            var inventories = inventoryResponse.IsSuccessStatusCode
                ? await inventoryResponse.Content.ReadFromJsonAsync<List<InventoryViewModel>>() ?? new List<InventoryViewModel>()
                : new List<InventoryViewModel>();

            ViewData["Product"] = product;
            ViewData["Inventories"] = inventories;
            ViewData["InventoriesLoaded"] = inventoryResponse.IsSuccessStatusCode;
            return PartialView("_AddToInventoryPartial", new AddToInventoryViewModel
            {
                ProductID = id,
                InventoryID = inventories.FirstOrDefault()?.InventoryID ?? 0
            });
        }

        // POST: Product/AddToInventory — crea la línea del producto en la bodega y, si hay cantidad, registra la carga inicial.
        // Son dos llamadas a la API. Si la segunda falla la línea ya existe (con 0 unidades); el mensaje lo dice y un reintento
        // reutiliza esa línea en vez de intentar crearla otra vez.
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToInventory(AddToInventoryViewModel model)
        {
            if (model.ProductID <= 0 || model.InventoryID <= 0)
                return Json(new { success = false, message = "No se pudo agregar al inventario.", errors = new[] { "Elegí una bodega activa." } });
            if (!ModelState.IsValid)
                return Json(new
                {
                    success = false,
                    message = "No se pudo agregar al inventario.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList()
                });

            var client = _httpClientFactory.CreateClient("BackendApi");

            int lineId = 0;
            var linesResponse = await client.GetAsync($"api/InventoryLine/inventory/{model.InventoryID}");
            if (linesResponse.IsSuccessStatusCode)
            {
                var lines = await linesResponse.Content.ReadFromJsonAsync<List<InventoryLineViewModel>>() ?? new List<InventoryLineViewModel>();
                lineId = lines.FirstOrDefault(l => l.ProductID == model.ProductID)?.InventoryLineID ?? 0;
            }

            if (lineId == 0)
            {
                var createResponse = await client.PostAsJsonAsync("api/InventoryLine",
                    new AddInventoryLineViewModel { InventoryID = model.InventoryID, ProductID = model.ProductID, QuantityRestock = model.QuantityRestock });
                if (!createResponse.IsSuccessStatusCode)
                    return await ErrorJsonAsync(createResponse, "No se pudo agregar el producto al inventario.");

                var createdLine = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (createdLine.ValueKind != JsonValueKind.Object || !createdLine.TryGetProperty("id", out var lineIdElement) || !lineIdElement.TryGetInt32(out lineId))
                    return Json(new { success = false, message = "No se pudo agregar el producto al inventario.", errors = new[] { "La API no devolvió la línea creada." } });
            }

            if (model.InitialQuantity > 0)
            {
                var loadResponse = await client.PostAsJsonAsync("api/InventoryTransaction/register", new RegisterInventoryTransactionViewModel
                {
                    InventoryLineID = lineId,
                    Type = TransactionType.CargaInicial,
                    TransactionQuantity = model.InitialQuantity,
                    Reason = "Carga inicial al crear el producto"
                });
                if (!loadResponse.IsSuccessStatusCode)
                {
                    var errors = await ApiErrorReader.ReadMessagesAsync(loadResponse, "No se pudo registrar la carga inicial.");
                    return Json(new
                    {
                        success = false,
                        lineCreated = true,
                        lineId,
                        message = "El producto ya está en la bodega con 0 unidades, pero no se pudo registrar la carga inicial.",
                        errors
                    });
                }
            }

            return Json(new { success = true, lineId, quantity = model.InitialQuantity });
        }

        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> EditPartial(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Product/{id}");

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var producto = await response.Content.ReadFromJsonAsync<ProductViewModel>();
            if (producto == null) return NotFound();

            var model = new UpdateProductViewModel
            {
                ProductCode = producto.ProductCode,
                ProductName = producto.ProductName,
                ProductDesc = producto.ProductDesc,
                categoria = producto.categoria,
                CostPrice = producto.CostPrice,
                Price = producto.Price,
                UnidadMedida = producto.UnidadMedida,
                StatusID = producto.StatusID
            };

            var statusResponse = await client.GetAsync("api/Status");
            var statusOptions = statusResponse.IsSuccessStatusCode
                ? await statusResponse.Content.ReadFromJsonAsync<List<StatusViewModel>>()
                : new List<StatusViewModel>();

            ViewData["ProductId"] = id;
            ViewData["StatusOptions"] = statusOptions ?? new List<StatusViewModel>();
            return PartialView("_EditProductPartial", model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateProductViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Product/{id}", model);

            if (!response.IsSuccessStatusCode)
                return await ErrorJsonAsync(response, "No se pudo actualizar el producto.");

            return Json(new { success = true });
        }

        // POST: ProductController/Inactivate/5 — sin form propio: trae el producto actual y lo reenvía con StatusID inactivo.
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inactivate(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var getResponse = await client.GetAsync($"api/Product/{id}");
            if (!getResponse.IsSuccessStatusCode)
                return Json(new { success = false, message = "Producto no encontrado." });

            var producto = await getResponse.Content.ReadFromJsonAsync<ProductViewModel>();
            if (producto == null)
                return Json(new { success = false, message = "Producto no encontrado." });

            var model = new UpdateProductViewModel
            {
                ProductCode = producto.ProductCode,
                ProductName = producto.ProductName,
                ProductDesc = producto.ProductDesc,
                categoria = producto.categoria,
                CostPrice = producto.CostPrice,
                Price = producto.Price,
                UnidadMedida = producto.UnidadMedida,
                StatusID = producto.StatusID
            };

            var response = await client.PutAsJsonAsync($"api/Product/{id}/inactivate", model);
            if (!response.IsSuccessStatusCode)
                return await ErrorJsonAsync(response, "No se pudo inactivar el producto.");

            return Json(new { success = true });
        }

        // POST: ProductController/ImportCatalog?dryRun=true — dispara la importación del catálogo del proveedor externo.
        // Con dryRun solo simula: devuelve el resumen sin guardar nada. El resumen del backend se reenvía tal cual al JS.
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCatalog(bool dryRun = true)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Product/import?dryRun={(dryRun ? "true" : "false")}", null);

            if (!response.IsSuccessStatusCode)
                return await ErrorJsonAsync(response, "No se pudo importar el catálogo.");

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return Json(new { success = true, result });
        }

        // ───────── CABYS (factura electrónica) ─────────

        // GET: Product/Cabys — todos los productos con su CABYS, para completar los que faltan
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Cabys(bool onlyMissing = false)
        {
            ViewData["OnlyMissing"] = onlyMissing;
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Product");
            var productos = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<ProductViewModel>>() ?? new()
                : new List<ProductViewModel>();
            return View(productos.OrderBy(p => p.ProductName).ToList());
        }

        // GET: Product/CabysSearch?q=casco — busca en el catálogo oficial; el JSON de la API se reenvía tal cual
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> CabysSearch(string? q)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Product/cabys/search?q={Uri.EscapeDataString(q ?? string.Empty)}");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo buscar en el catálogo CABYS.") });
            return Json(new { success = true, items = await response.Content.ReadFromJsonAsync<JsonElement>() });
        }

        // POST: Product/SetCabys — asigna o quita (vacío) el CABYS de un producto
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCabys(int id, string? cabysCode)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Product/{id}/cabys", new { cabysCode = string.IsNullOrWhiteSpace(cabysCode) ? null : cabysCode.Trim() });
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo guardar el CABYS.") });
        }

        // POST: Product/SetCabysBulk — el mismo CABYS para una categoría; responde cuántos productos cambió
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCabysBulk(int category, string? cabysCode, bool onlyEmpty = true)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync("api/Product/cabys/bulk", new { category, cabysCode, onlyEmpty });
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo asignar el CABYS.") });
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return Json(new { success = true, count = result.GetProperty("id").GetInt32() });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.DeleteAsync($"api/Product/{id}");

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : await ErrorJsonAsync(response, "No se pudo eliminar el producto.");
        }

        // Error de la API como JSON para el SweetAlert: message = título, errors = cada motivo (validaciones incluidas).
        private async Task<IActionResult> ErrorJsonAsync(HttpResponseMessage response, string title) =>
            Json(new { success = false, message = title, errors = await ApiErrorReader.ReadMessagesAsync(response, title) });
    }
}
