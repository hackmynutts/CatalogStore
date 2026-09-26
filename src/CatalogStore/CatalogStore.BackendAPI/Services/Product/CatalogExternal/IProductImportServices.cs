using CatalogStore.BackendAPI.DTO.CatalogExternal;

namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public interface IProductImportServices
    {
        /// <summary>
        /// Importa el catálogo de productos del proveedor externo. Con dryRun = true calcula el resumen sin guardar nada.
        /// </summary>
        Task<ProductImportResultDTO> ImportAsync(bool dryRun, string requestedBy, CancellationToken cancellationToken = default);
    }
}
