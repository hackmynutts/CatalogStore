using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Goals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace CatalogStore.UI.Controllers
{
    // Metas mensuales de ventas: "Mis metas" (cada usuario) y "Metas del equipo" (admin), que llaman a api/Goal.
    // Las acciones POST devuelven JSON { success, message } para goals-team.js.
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class GoalsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public GoalsController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        // GET: Goals/Mine — "Mis metas" vive en el perfil (Account/Details); esta dirección queda por los enlaces viejos.
        public IActionResult Mine(int? year, int? month) =>
            RedirectToAction("Details", "Account", new { year, month }, "metas");

        // GET: Goals/MineDetail?year=2026&month=10 — las ventas y abonos que suman en MIS metas de ese mes.
        public async Task<IActionResult> MineDetail(int? year, int? month)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Goal/me/detail" + PeriodQuery(year, month));
            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Details", "Account", new { year, month }, "metas");

            var model = await response.Content.ReadFromJsonAsync<MyGoalDetailViewModel>();
            return model is null ? RedirectToAction("Details", "Account", new { year, month }, "metas") : View(model);
        }

        // GET: Goals/Team?year=2026&month=10 — todos los vendedores (solo admin)
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Team(int? year, int? month)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Goal/team" + PeriodQuery(year, month));
            if (!response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Team));

            var model = await response.Content.ReadFromJsonAsync<TeamGoalsViewModel>();
            return model is null ? RedirectToAction(nameof(Team)) : View(model);
        }

        // POST: Goals/Save — los montos llegan como texto invariante ("1500.50") para no depender de la cultura.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Save(string? seller, int year, int month, string? salesAmountTarget, string? collectedAmountTarget, string? salesCountTarget, string? rowVersion)
        {
            if (!TryAmount(salesAmountTarget, out var sold) || !TryAmount(collectedAmountTarget, out var collected))
                return Json(new { success = false, message = "Los montos de la meta no son válidos." });
            if (!int.TryParse(string.IsNullOrWhiteSpace(salesCountTarget) ? "0" : salesCountTarget, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                return Json(new { success = false, message = "La cantidad de ventas tiene que ser un número entero." });

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync("api/Goal", new
            {
                seller,
                year,
                month,
                salesAmountTarget = sold,
                collectedAmountTarget = collected,
                salesCountTarget = count,
                rowVersion = string.IsNullOrWhiteSpace(rowVersion) ? null : rowVersion
            });

            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo guardar la meta.") });
        }

        // POST: Goals/CopyPrevious — copia las metas del mes anterior a quienes todavía no tienen
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> CopyPrevious(int year, int month)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/Goal/copy-previous", new { year, month });
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudieron copiar las metas.") });

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var copied = doc.RootElement.TryGetProperty("copied", out var c) ? c.GetInt32() : 0;
            return Json(new { success = true, copied });
        }

        // year y month van juntos o ninguno; si llega uno solo se ignora y se muestra el mes en curso.
        private static string PeriodQuery(int? year, int? month) =>
            year.HasValue && month.HasValue ? $"?year={year.Value}&month={month.Value}" : string.Empty;

        private static bool TryAmount(string? text, out decimal value)
        {
            value = 0m;
            return string.IsNullOrWhiteSpace(text)
                || decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }
    }
}
