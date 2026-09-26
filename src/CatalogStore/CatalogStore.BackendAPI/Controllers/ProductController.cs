using CatalogStore.BackendAPI.DTO.Product;
using CatalogStore.BackendAPI.Models.Product;
using CatalogStore.BackendAPI.Services.Product;
using CatalogStore.BackendAPI.Services.Product.CatalogExternal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly IProductServices _productServices;
        private readonly IProductImportServices _importServices;
        public ProductController(IProductServices productServices, IProductImportServices importServices)
        {
            _productServices = productServices;
            _importServices = importServices;
        }
        // GET: api/<ProductController>
        [HttpGet]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> GetAll()
        {
            List<Product> lista = await _productServices.GetAllProductsAsync();
            return Ok(lista);
        }
        // GET: api/<ProductController>
        [HttpGet("active")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetActive()
        {
            List<Product> lista = await _productServices.GetActiveProductsAsync();
            return Ok(lista);
        }

        // GET api/<ProductController>/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> Get(int id)
        {
            Product  producto = await _productServices.GetProductAsync(id);
            if (producto == null) return NotFound();
            return Ok(producto);
        }

        // POST api/<ProductController>
        [HttpPost]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Post([FromBody] AddProductDTO add)
        {
            add.CreatedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            var res = await _productServices.AddAsync(add);
            return CreatedAtAction(nameof(Get), new { id = res }, add);
        }

        // POST api/Product/import?dryRun=true
        [HttpPost("import")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Import([FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        {
            var requestedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            try
            {
                var result = await _importServices.ImportAsync(dryRun, requestedBy, cancellationToken);
                return Ok(result);
            }
            catch (ProductImportInProgressException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "No se pudo obtener el catálogo del proveedor externo. Revise la configuración o intente más tarde." });
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "El proveedor externo tardó demasiado en responder." });
            }
            catch (JsonException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "El proveedor externo devolvió una respuesta inválida." });
            }
        }

        // PUT api/<ProductController>/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Put(int id, [FromBody] UpdateProductDTO edit)
        {
            edit.ModifiedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            bool updated = await _productServices.UpdateAsync(id, edit);
            if(!updated) return NotFound();
            return Ok(edit);
        }
        // PUT api/<ProductController>/5
        [HttpPut("{id}/inactivate")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> PutInactivate(int id, [FromBody] UpdateProductDTO edit)
        {
            edit.ModifiedBy = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            bool updated = await _productServices.InactivateAsync(id, edit);
            if (!updated) return NotFound();
            return Ok(edit);
        }

        // DELETE api/<ProductController>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _productServices.DeleteAsync(id);
            if (!deleted) return NotFound();
            return Ok();
        }
    }
}
