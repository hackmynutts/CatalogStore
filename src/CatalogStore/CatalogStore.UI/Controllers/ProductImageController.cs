using CatalogStore.UI.Models.ProductImage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace CatalogStore.UI.Controllers
{
    [Authorize]
    public class ProductImageController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public ProductImageController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private string ResolveUrl(string url) => _configuration["BackendApi:PublicBaseUrl"]!.TrimEnd('/') + url;

        // GET: ProductImageController/ByProduct/5 — para refrescar la galería por AJAX después de subir/borrar
        [HttpGet]
        public async Task<IActionResult> ByProduct(int id)
        {
            return Json(await GetImagesForProduct(id));
        }

        // GET: ProductImageController/Get/5 — para el modal de pantalla completa
        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/ProductImage/{id}");

            if (!response.IsSuccessStatusCode)
                return NotFound();

            var imagen = await response.Content.ReadFromJsonAsync<ProductImageViewModel>();
            if (imagen == null) return NotFound();

            imagen.Url = ResolveUrl(imagen.Url);
            return Json(imagen);
        }

        // GET: ProductImageController/Index — ventana administrativa con todas las imágenes del sistema
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/ProductImage");

            if (!response.IsSuccessStatusCode)
                return View(new List<ProductImageViewModel>());

            var imagenes = await response.Content.ReadFromJsonAsync<List<ProductImageViewModel>>() ?? new List<ProductImageViewModel>();
            foreach (var img in imagenes)
                img.Url = ResolveUrl(img.Url);

            return View(imagenes);
        }

        // GET: ProductImageController/ManagePartial/5 — contenido del modal de gestión de imágenes
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> ManagePartial(int id)
        {
            var imagenes = await GetImagesForProduct(id);
            ViewData["ProductId"] = id;
            return PartialView("_ManageImagesPartial", imagenes);
        }

        private async Task<List<ProductImageViewModel>> GetImagesForProduct(int productId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/ProductImage/product/{productId}");

            if (!response.IsSuccessStatusCode)
                return new List<ProductImageViewModel>();

            var imagenes = await response.Content.ReadFromJsonAsync<List<ProductImageViewModel>>() ?? new List<ProductImageViewModel>();
            foreach (var img in imagenes)
                img.Url = ResolveUrl(img.Url);

            return imagenes;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No se recibió ningún archivo." });

            var client = _httpClientFactory.CreateClient("BackendApi");

            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(streamContent, "image", file.FileName);

            var response = await client.PostAsync($"api/ProductImage/{id}", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return Json(new { success = false, message = "No se pudo subir la imagen.", details = errorBody });
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.DeleteAsync($"api/ProductImage/{id}");

            return Json(new { success = response.IsSuccessStatusCode });
        }
    }
}
