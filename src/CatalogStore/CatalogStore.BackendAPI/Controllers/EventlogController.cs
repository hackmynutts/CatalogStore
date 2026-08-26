using CatalogStore.BackendAPI.Services.EventLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EventlogController : ControllerBase
    {
        private readonly IEventlogServices _eventlogService;
        public EventlogController(IEventlogServices eventlogService)
        {
            _eventlogService = eventlogService;
        }
        // GET: api/<EventlogController>
        [Authorize(Roles = "Admin,AdminIT")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var logs = await _eventlogService.GetAllEventsAsync();
            return Ok(logs);
        }

        // POST api/<EventlogController>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] DTO.EventLogs.AddEventlogDTO log)
        {
            await _eventlogService.AddAsync(log);
            return Ok();
        }
    }
}
