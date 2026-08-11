using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.BackendAPI.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
