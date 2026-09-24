using CatalogStore.BackendAPI.DTO.CatalogExternal;

namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public interface ICatalogExternalServices
    {
        Task<ExternalCatalogPageDTO> GetProductsPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
