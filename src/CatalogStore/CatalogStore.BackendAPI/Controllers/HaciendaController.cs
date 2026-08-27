using CatalogStore.BackendAPI.Services.Client.Hacienda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class HaciendaController : ControllerBase
    {
        private readonly IHaciendaServices _haciendaServices;
        public HaciendaController(IHaciendaServices haciendaServices)
        {
            _haciendaServices = haciendaServices;
        }

        [HttpGet("lookup/{identification}")]
        public async Task<IActionResult> Lookup(string identification)
        {
            var result = await _haciendaServices.LookupAsync(identification);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
