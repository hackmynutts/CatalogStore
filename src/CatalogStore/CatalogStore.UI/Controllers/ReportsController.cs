using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace CatalogStore.UI.Controllers
{
    // Reportes en Excel. La página arma los filtros y Download pide el archivo a api/Report y se lo entrega al navegador.
    // Ventas, cuentas por cobrar y cobros los ve también el vendedor (la API lo limita a sus propias ventas);
    // ventas por producto, metas e inventario traen costos y son solo de administradores.
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class ReportsController : Controller
    {
        private const string XlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        private readonly IHttpClientFactory _httpClientFactory;
        public ReportsController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        // GET: Reports
        public async Task<IActionResult> Index()
        {
            var sellers = new List<UserViewModel>();
            if (User.IsInRole("Admin") || User.IsInRole("AdminIT"))
            {
                var client = _httpClientFactory.CreateClient("BackendApi");
                var response = await client.GetAsync("api/User");
                if (response.IsSuccessStatusCode)
                    sellers = (await response.Content.ReadFromJsonAsync<List<UserViewModel>>() ?? new())
                        .Where(u => u.Roles.Contains("Vendedor")).OrderBy(u => string.IsNullOrWhiteSpace(u.FullName) ? u.Email : u.FullName).ToList();
            }
            ViewData["Sellers"] = sellers;
            return View();
        }

        // GET: Reports/Download?key=sales&from=2026-10-01&to=2026-10-31&seller=correo&includeVoided=true
        // goals usa period=2026-10 (el selector de mes del navegador).
        public async Task<IActionResult> Download(string key, string? from, string? to, string? seller, bool includeVoided, string? period)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(from)) query.Add($"from={Uri.EscapeDataString(from)}");
            if (!string.IsNullOrWhiteSpace(to)) query.Add($"to={Uri.EscapeDataString(to)}");
            if (!string.IsNullOrWhiteSpace(seller)) query.Add($"seller={Uri.EscapeDataString(seller)}");
            if (includeVoided) query.Add("includeVoided=true");
            if (DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                query.Add($"year={month.Year}&month={month.Month}");

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Report/{Uri.EscapeDataString(key ?? string.Empty)}" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty));
            if (!response.IsSuccessStatusCode)
            {
                TempData["ReportError"] = await ApiErrorReader.ReadAsync(response, "No se pudo generar el reporte.");
                return RedirectToAction(nameof(Index));
            }

            var disposition = response.Content.Headers.ContentDisposition;
            var name = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "reporte.xlsx";
            return File(await response.Content.ReadAsByteArrayAsync(), XlsxMime, name);
        }
    }
}
