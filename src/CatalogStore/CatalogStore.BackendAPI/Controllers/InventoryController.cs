using CatalogStore.BackendAPI.DTO.Inventory;
using CatalogStore.BackendAPI.Models.Inventory;
using CatalogStore.BackendAPI.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryServices _inventoryServices;
        public InventoryController(IInventoryServices inventoryServices)
        {
            _inventoryServices = inventoryServices;
        }
        // GET: api/<InventoryController>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            List<Inventory> inventoryList = await _inventoryServices.GetAllInventoriesAsync();
            return Ok(inventoryList);
        }
        // GET: api/<InventoryController>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            List<Inventory> inventoryList = await _inventoryServices.GetActiveInventoriesAsync();
            return Ok(inventoryList);
        }

        // GET api/<InventoryController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            Inventory inventory = await _inventoryServices.GetInventoryAsync(id);
            if (inventory == null) return NotFound();
            return Ok(inventory);
        }

        // POST api/<InventoryController>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] AddInventoryDTO inventory)
        {
            inventory.CreatedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            int id = await _inventoryServices.AddAsync(inventory);
            return CreatedAtAction(nameof(Get), new { id = id }, new { Id = id });
        }

        // PUT api/<InventoryController>/5
        [HttpPut("{id}/inactivate")]
        public async Task<IActionResult> PutInactivate(int id, [FromBody] UpdateInventoryDTO inventory)
        {
            inventory.ModifiedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            bool inactivate = await _inventoryServices.InactivateAsync(inventory);
            if (!inactivate) return NotFound();
            return Ok(inventory);
        }
        // PUT api/<InventoryController>/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] UpdateInventoryDTO inventory)
        {
            inventory.ModifiedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            bool updated = await _inventoryServices.UpdateAsync(inventory);
            if (!updated) return NotFound();
            return Ok(inventory);
        }

        // DELETE api/<InventoryController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            bool deleted = await _inventoryServices.DeleteAsync(id);
            if (!deleted) return NotFound();
            return Ok();
        }
    }
}
