using CatalogStore.BackendAPI.DTO.Product;
using CatalogStore.BackendAPI.Services.Product;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace CatalogStore.BackendAPI.Controllers
{
    // Catálogo para la página pública (sin iniciar sesión). Solo productos activos con precio y sin ningún dato comercial:
    // nombre, código, categoría y foto. El resultado se guarda 60 s para que muchas visitas no peguen a la base,
    // y hay un tope global de peticiones porque desde aquí no se ve la IP real del visitante (siempre llega la de la UI).
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    [EnableRateLimiting(PublicCatalogPolicy)]
    public class PublicCatalogController : ControllerBase
    {
        public const string PublicCatalogPolicy = "PublicCatalog";
        private const string CacheKey = "public-catalog";
        private readonly IProductServices _productServices;
        private readonly IMemoryCache _cache;
        public PublicCatalogController(IProductServices productServices, IMemoryCache cache)
        {
            _productServices = productServices;
            _cache = cache;
        }

        // GET api/PublicCatalog
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var items = await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                return await _productServices.GetPublicCatalogAsync();
            });
            return Ok(items ?? new List<PublicCatalogItemDTO>());
        }
    }
}
