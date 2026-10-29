using CatalogStore.BackendAPI.DTO.Product;
using CatalogStore.BackendAPI.Models.Product;
using CatalogStore.BackendAPI.Services.Product;
using CatalogStore.BackendAPI.Services.Product.Cabys;
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
        private readonly ICabysServices _cabysServices;
        public ProductController(IProductServices productServices, IProductImportServices importServices, ICabysServices cabysServices)
        {
            _productServices = productServices;
            _importServices = importServices;
            _cabysServices = cabysServices;
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
            foreach (var p in lista) HideCostFromSellers(p);
            return Ok(lista);
        }

        // GET api/<ProductController>/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> Get(int id)
        {
            Product  producto = await _productServices.GetProductAsync(id);
            if (producto == null) return NotFound();
            HideCostFromSellers(producto);
            return Ok(producto);
        }
        // GET api/<ProductController>/5
        [HttpGet("code/{code}")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetByCode(string code)
        {
            Product  producto = await _productServices.GetByProductCode(code);
            if (producto == null) return NotFound();
            HideCostFromSellers(producto);
            return Ok(producto);
        }

        // El costo es solo para administradores. Es seguro modificar la entidad porque estas lecturas son AsNoTracking:
        // nada de esto vuelve a la base.
        private void HideCostFromSellers(Product product)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("AdminIT")) product.CostPrice = null;
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

        // GET api/Product/availability — { productId: unidades disponibles } para las tarjetas del catálogo
        [HttpGet("availability")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetAvailability() => Ok(await _productServices.GetAvailabilityAsync());

        // GET api/Product/cabys/search?q=casco — busca en el catálogo oficial CABYS de Hacienda
        [HttpGet("cabys/search")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> SearchCabys([FromQuery] string? q, CancellationToken cancellationToken)
        {
            try
            {
                var items = await _cabysServices.SearchAsync(q ?? string.Empty, cancellationToken);
                return Ok(items.Select(i => new CabysItemDTO { Code = i.Code, Description = i.Description, TaxRate = i.TaxRate, Category = i.Category }));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "No se pudo consultar el catálogo CABYS de Hacienda. Intentá de nuevo en unos minutos." });
            }
        }

        // PUT api/Product/5/cabys — asignar o quitar (null) el CABYS de un producto
        [HttpPut("{id:int}/cabys")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> PutCabys(int id, [FromBody] SetCabysDTO dto) =>
            ToResponse(await _productServices.SetCabysAsync(id, dto.CabysCode, CurrentUser));

        // PUT api/Product/cabys/bulk — el mismo CABYS para una categoría; responde { id } con cuántos se actualizaron
        [HttpPut("cabys/bulk")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> PutCabysBulk([FromBody] BulkCabysDTO dto) =>
            ToResponse(await _productServices.SetCabysByCategoryAsync(dto, CurrentUser));

        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";

        private IActionResult ToResponse(CatalogStore.BackendAPI.DTO.Common.OperationResult result)
        {
            if (result.Success)
                return Ok(new { id = result.Id });
            return result.Error == ProductServices.NotFoundMessage
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
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
