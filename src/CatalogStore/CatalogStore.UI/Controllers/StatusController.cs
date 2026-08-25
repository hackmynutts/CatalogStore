using CatalogStore.UI.Models.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class StatusController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public StatusController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IActionResult> Index()
    {
        var client = _httpClientFactory.CreateClient("BackendApi");
        var response = await client.GetAsync("api/Status");

        if (!response.IsSuccessStatusCode)
            return View(new List<StatusViewModel>());

        var statuses = await response.Content.ReadFromJsonAsync<List<StatusViewModel>>();
        return View(statuses ?? new List<StatusViewModel>());
    }

    [Authorize(Roles = "Admin,AdminIT")]
    public IActionResult CreatePartial()
    {
        return PartialView("_CreateStatusPartial", new AddStatusViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,AdminIT")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddStatusViewModel model)
    {
        model.CreatedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Desconocido";

        var client = _httpClientFactory.CreateClient("BackendApi");
        var response = await client.PostAsJsonAsync("api/Status", model);

        if (!response.IsSuccessStatusCode)
            return Json(new { success = false, message = "No se pudo crear el estado." });

        return Json(new { success = true });
    }

    [Authorize(Roles = "Admin,AdminIT")]
    public async Task<IActionResult> EditPartial(int id)
    {
        var client = _httpClientFactory.CreateClient("BackendApi");
        var response = await client.GetAsync($"api/Status/{id}");

        if (!response.IsSuccessStatusCode)
            return NotFound();

        var status = await response.Content.ReadFromJsonAsync<StatusViewModel>();
        if (status == null)
            return NotFound();

        var model = new UpdateStatusViewModel
        {
            StatusID = status.StatusID,
            Name = status.Name
        };

        return PartialView("_EditStatusPartial", model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,AdminIT")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateStatusViewModel model)
    {
        model.UpdatedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Desconocido";

        var client = _httpClientFactory.CreateClient("BackendApi");
        var response = await client.PutAsJsonAsync($"api/Status/{id}", model);

        if (!response.IsSuccessStatusCode)
            return Json(new { success = false, message = "No se pudo actualizar el estado." });

        return Json(new { success = true });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,AdminIT")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var client = _httpClientFactory.CreateClient("BackendApi");
        await client.DeleteAsync($"api/Status/{id}");

        return RedirectToAction(nameof(Index));
    }
}
