using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Product;

namespace CatalogStore.BackendAPI.Services.Product
{
    public interface IProductServices
    {
        Task<List<Models.Product.Product>> GetAllProductsAsync();
        Task<List<Models.Product.Product>> GetActiveProductsAsync();
        Task<Models.Product.Product> GetProductAsync(int id);
        Task<Models.Product.Product> GetByProductCode(string code);
        Task<int> AddAsync(AddProductDTO dto);
        Task<bool> UpdateAsync(int id, UpdateProductDTO dto);
        Task<bool> InactivateAsync(int id, UpdateProductDTO dto);
        Task<bool> DeleteAsync(int id);
        // Disponible para vender por producto (existencias menos reservado).
        Task<Dictionary<int, int>> GetAvailabilityAsync();
        // Lo que ve el público (sin precios ni cantidades). Ver PublicCatalogController.
        Task<List<DTO.Product.PublicCatalogItemDTO>> GetPublicCatalogAsync();
        // CABYS para la factura electrónica. Se valida contra el catálogo oficial de Hacienda antes de guardar.
        Task<OperationResult> SetCabysAsync(int id, string? cabysCode, string user);
        // El mismo CABYS para todos los productos de una categoría. Id del resultado = cuántos se actualizaron.
        Task<OperationResult> SetCabysByCategoryAsync(BulkCabysDTO dto, string user);
    }
}
