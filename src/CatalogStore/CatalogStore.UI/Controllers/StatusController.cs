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
}