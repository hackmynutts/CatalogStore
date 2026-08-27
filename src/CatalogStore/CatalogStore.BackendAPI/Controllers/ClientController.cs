using CatalogStore.BackendAPI.DTO.Client;
using CatalogStore.BackendAPI.Services.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class ClientController : ControllerBase
    {
        private readonly IClientServices _services;
        public ClientController(IClientServices clientServices) { _services = clientServices; }
        // GET: api/<ClientController>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var clients = await _services.GetAllClientsAsync();
            return Ok(clients);
        }
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var clients = await _services.GetActiveClientsAsync();
            return Ok(clients);
        }

        // GET api/<ClientController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var client = await _services.GetClientAsync(id);
            if (client == null) return NotFound();
            return Ok(client);
        }
        // GET api/<ClientController>/5
        [HttpGet("lookup")]
        public async Task<IActionResult> GetLookup(string identification)
        {
            var nombre = await _services.LookupAsync(identification);
            if (nombre == null) return NotFound();
            return Ok(new { nombre });
        }

        // POST api/<ClientController>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] AddClientDTO cliente)
        {
            cliente.CreatedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            var id = await _services.AddAsync(cliente);
            return CreatedAtAction(nameof(Get), new { id }, cliente);
        }

        // PUT api/<ClientController>/5
        [HttpPut("{id}/update")]
        public async Task<IActionResult> Put(int id, [FromBody] UpdateClientDTO cliente)
        {
            cliente.ModifiedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            var updated = await _services.UpdateAsync(id,cliente);
            if(!updated) return NotFound();
            return Ok(cliente);
        }
        [HttpPut("{id}/inactivate")]
        public async Task<IActionResult> PutInactivate(int id, [FromBody] UpdateClientDTO cliente)
        {
            cliente.ModifiedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            var updated = await _services.InactivateAsync(id,cliente);
            if(!updated) return NotFound();
            return Ok(cliente);
        }

        // DELETE api/<ClientController>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _services.DeleteAsync(id);
            if(!deleted) return NotFound();
            return Ok();
        }
    }
}
