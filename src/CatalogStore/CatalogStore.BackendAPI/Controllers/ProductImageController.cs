using CatalogStore.BackendAPI.Models.ProductImage;
using CatalogStore.BackendAPI.Services.ProductImage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductImageController : ControllerBase
    {
        private readonly IProductImageServices _productImageServices;
        public ProductImageController(IProductImageServices productImageServices)
        {
            _productImageServices = productImageServices;
        }
        // GET: api/<ProductImageController>
        [HttpGet]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> GetAll()
        {
            List<ProductImage> lista = await _productImageServices.GetAllProductImagesAsync();
            return Ok(lista);
        }
        [HttpGet("product/{productId}")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> GetAllByProductId(int productId)
        {
            List<ProductImage> lista = await _productImageServices.GetProductImagesByProductIdAsync(productId);
            return Ok(lista);
        }

        // GET api/<ProductImageController>/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,AdminIT,Vendedor")]
        public async Task<IActionResult> Get(int id)
        {
            ProductImage productImage = await _productImageServices.GetProductImageByIdAsync(id);
            if (productImage == null) return NotFound();
            return Ok(productImage);
        }

        // POST api/<ProductImageController>
        [HttpPost("{productid}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Post(int productid, IFormFile image)
        {
            string createdby = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
            int res = await _productImageServices.UploadAsync(productid, image, createdby);

            ProductImage productImage = await _productImageServices.GetProductImageByIdAsync(res);
            return CreatedAtAction(nameof(Get), new { id = res }, productImage); 
        }

        // DELETE api/<ProductImageController>/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Delete(int id)
        {
            ProductImage productImage = await _productImageServices.GetProductImageByIdAsync(id);
            if (productImage == null) return NotFound();
            var deleted = await _productImageServices.DeleteAsync(id); 
            if (!deleted) return NotFound();
            return Ok();
        }
    }
}
