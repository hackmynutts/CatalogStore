using CatalogStore.UI.Models.Eventlog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.UI.Controllers
{
    [Authorize(Roles = "Admin,AdminIT")]
    public class EventlogController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public EventlogController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // GET: EventlogController
        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Eventlog");

            if (!response.IsSuccessStatusCode)
                return View(new List<EventlogViewModel>());

            var logs = await response.Content.ReadFromJsonAsync<List<EventlogViewModel>>();
            return View(logs ?? new List<EventlogViewModel>());
        }
    }
}
