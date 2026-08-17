using CatalogStore.BackendAPI.Services.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StatusController : ControllerBase
    {
        private readonly IStatusServices _statusService;
        public StatusController(IStatusServices statusService)
        {
            _statusService = statusService;
        }
        // GET: api/<StatusController>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var statuses = await _statusService.GetAllStatusesAsync();
            return Ok(statuses);
        }

        // GET api/<StatusController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var status = await _statusService.GetStatusAsync(id);
            if (status == null) return NotFound();
            return Ok(status);
        }

        // POST api/<StatusController>
        [Authorize(Roles = "Admin,AdminIT")]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] DTO.Status.AddStatusDTO status)
        {
            var id = await _statusService.AddAsync(status);
            return CreatedAtAction(nameof(Get), new { id }, status);
        }

        // PUT api/<StatusController>/5
        [Authorize(Roles = "Admin,AdminIT")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] DTO.Status.UpdateStatusDTO status)
        {
            status.StatusID = id;
            var updated = await _statusService.UpdateAsync(status);
            if (!updated) return NotFound();
            return Ok(status);
        }

        // DELETE api/<StatusController>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _statusService.DeleteAsync(id);
            if (!deleted) return NotFound();
            return Ok();
        }
    }
}
