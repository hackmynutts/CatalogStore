using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Product
{
    public interface IProductRepository
    {
        Task<List<Models.Product.Product>> GetAllProductsAsync();
        Task<List<Models.Product.Product>> GetActiveProductsAsync();
        Task<Models.Product.Product> GetProductAsync(int id);
        Task<Models.Product.Product> GetByProductCode(string code);
        Task<int> AddAsync(Models.Product.Product prod);
        Task<bool> UpdateAsync(Models.Product.Product prod);
        Task<bool> DeleteAsync(int id);
        // Varios productos en una sola consulta, sin seguimiento (solo lectura). Lo usa el servicio de órdenes al completar.
        Task<Dictionary<int, Models.Product.Product>> GetByIdsAsync(IEnumerable<int> productIds);
        // Soporte para la importación por lotes: las entidades se devuelven con seguimiento y se guardan juntas.
        Task<Dictionary<int, Models.Product.Product>> GetByExternalIdsAsync(IEnumerable<int> externalIds);
        Task AddRangeAsync(IEnumerable<Models.Product.Product> products);
        // Con seguimiento: para cambiar solo el CABYS sin reescribir el resto del producto.
        Task<Models.Product.Product?> GetTrackedAsync(int id);
        // Unidades que se pueden vender ahora (existencias menos reservado), por producto. Un producto sin línea de inventario no aparece.
        Task<Dictionary<int, int>> GetAvailabilityAsync();
        // Catálogo público: productos activos con precio y su primera foto. Sin datos comerciales ni existencias.
        Task<List<DTO.Product.PublicCatalogItemDTO>> GetPublicCatalogAsync();
        // Con seguimiento: productos de una categoría (todos, o solo los que no tienen CABYS).
        Task<List<Models.Product.Product>> GetTrackedByCategoryAsync(Models.Product.categoryType category, bool onlyWithoutCabys);
        Task<int> SaveChangesAsync();
    }
}
