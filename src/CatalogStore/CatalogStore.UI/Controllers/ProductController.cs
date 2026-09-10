using CatalogStore.UI.Models.Product;
using CatalogStore.UI.Models.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            ResolveImageUrls(productos);
            return View(productos);
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
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo crear el producto.", details = errorBody });
            }

            return Json(new { success = true });
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
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo actualizar el producto.", details = errorBody });
            }

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
                Price = producto.Price,
                UnidadMedida = producto.UnidadMedida,
                StatusID = producto.StatusID
            };

            var response = await client.PutAsJsonAsync($"api/Product/{id}/inactivate", model);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "No se pudo inactivar el producto." });

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            await client.DeleteAsync($"api/Product/{id}");

            return RedirectToAction(nameof(Index));
        }
    }
}
