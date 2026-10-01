using CatalogStore.BackendAPI.DTO.InventoryLines;
using CatalogStore.BackendAPI.Services.InventoryLines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT")]
    public class InventoryLineController : ControllerBase
    {
        private readonly IInventoryLineServices _inventoryLineServices;
        public InventoryLineController(IInventoryLineServices inventoryLineServices)
        {
            _inventoryLineServices = inventoryLineServices;
        }

        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

        // GET api/InventoryLine/inventory/5 — líneas de una bodega
        [HttpGet("inventory/{inventoryId}")]
        public async Task<IActionResult> GetByInventory(int inventoryId) =>
            Ok(await _inventoryLineServices.GetByInventoryAsync(inventoryId));

        // GET api/InventoryLine/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var line = await _inventoryLineServices.GetByIdAsync(id);
            return line is null ? NotFound() : Ok(line);
        }

        // POST api/InventoryLine
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] AddInventoryLineDTO dto)
        {
            dto.CreatedBy = CurrentUser;
            var result = await _inventoryLineServices.AddAsync(dto);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return CreatedAtAction(nameof(Get), new { id = result.Id }, new { id = result.Id });
        }

        // PUT api/InventoryLine/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] UpdateInventoryLineDTO dto)
        {
            dto.InventoryLineID = id;
            dto.ModifiedBy = CurrentUser;
            var result = await _inventoryLineServices.UpdateAsync(dto);
            return result.Success ? Ok() : BadRequest(new { message = result.Error });
        }

        // PUT api/InventoryLine/5/inactivate
        [HttpPut("{id}/inactivate")]
        public async Task<IActionResult> Inactivate(int id)
        {
            var result = await _inventoryLineServices.InactivateAsync(id, CurrentUser);
            return result.Success ? Ok() : BadRequest(new { message = result.Error });
        }
    }
}