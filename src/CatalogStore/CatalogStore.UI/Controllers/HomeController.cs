using CatalogStore.UI.Documents;
using CatalogStore.UI.Models;
using CatalogStore.UI.Models.Home;
using CatalogStore.UI.Models.Order;
using QuestPDF.Fluent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace CatalogStore.UI.Controllers
{
    public class HomeController : Controller
    {
        public const string ConsultationPolicy = "ConsultationPdf";
        // La lista de consulta es pública: se acota lo que un visitante puede pedir.
        private const int MaxLines = 80, MaxCustomerLength = 60;
        private const string CatalogCacheKey = "public-catalog";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        public HomeController(IHttpClientFactory httpClientFactory, IMemoryCache cache, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: Home/Catalog — el catálogo público (sin precios) en su propia página.
        public async Task<IActionResult> Catalog()
        {
            return View("PublicCatalog", await LoadPublicCatalogAsync());
        }

        // POST: Home/ConsultationPdf — items = "12,15" (ids de producto). Los nombres salen del catálogo del servidor, no de lo que mande el navegador.
        [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(ConsultationPolicy)]
        public async Task<IActionResult> ConsultationPdf(string? items, string? customer, [FromServices] IWebHostEnvironment env)
        {
            var catalog = (await LoadPublicCatalogAsync()).ToDictionary(p => p.Id);
            var lines = new List<PublicCatalogItem>();
            foreach (var part in (items ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxLines))
                if (int.TryParse(part, out var id) && catalog.TryGetValue(id, out var item) && lines.All(l => l.Id != id))
                    lines.Add(item);
            if (lines.Count == 0) return BadRequest("La lista está vacía.");

            var name = string.IsNullOrWhiteSpace(customer) ? null : customer.Trim();
            if (name is { Length: > MaxCustomerLength }) name = name[..MaxCustomerLength];

            var logoPath = env.WebRootPath is null ? null : Path.Combine(env.WebRootPath, "img", "logos", "logo_pequeño-removebg-preview.png");
            var contact = string.Join(" · ", new[] { _configuration["Landing:WhatsApp"], _configuration["Landing:Email"] }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var document = new ConsultationPdfDocument(lines, name, OrderDocs.LocalNow(), contact,
                logoPath is not null && System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null);
            return File(document.GeneratePdf(), "application/pdf", $"Lista-consulta-{OrderDocs.LocalNow():yyyyMMdd}.pdf");
        }

        // Productos del catálogo público, guardados 60 s para que las visitas no pidan todo a la API cada vez.
        // Si la API no responde, la landing se muestra igual sin la sección de catálogo.
        private async Task<List<PublicCatalogItem>> LoadPublicCatalogAsync()
        {
            if (_cache.TryGetValue(CatalogCacheKey, out List<PublicCatalogItem>? cached) && cached is not null) return cached;
            try
            {
                // Cliente sin JwtDelegatingHandler: la landing es anónima y no debe mandar ninguna sesión.
                var client = _httpClientFactory.CreateClient("BackendApiAuth");
                var response = await client.GetAsync("api/PublicCatalog");
                if (!response.IsSuccessStatusCode) return new List<PublicCatalogItem>();
                var items = await response.Content.ReadFromJsonAsync<List<PublicCatalogItem>>() ?? new();
                var baseUrl = (_configuration["BackendApi:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
                foreach (var item in items.Where(i => !string.IsNullOrEmpty(i.ImageUrl))) item.ImageUrl = baseUrl + item.ImageUrl;
                _cache.Set(CatalogCacheKey, items, TimeSpan.FromSeconds(60));
                return items;
            }
            catch (HttpRequestException) { return new List<PublicCatalogItem>(); }
            catch (TaskCanceledException) { return new List<PublicCatalogItem>(); }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Route("Home/StatusCode/{code:int}")]
        public IActionResult StatusCode(int code)
        {
            ViewData["StatusCode"] = code;
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
