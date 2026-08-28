using CatalogStore.BackendAPI.DTO.Product;

namespace CatalogStore.BackendAPI.Services.Product
{
    public interface IProductServices
    {
        Task<List<Models.Product.Product>> GetAllProductsAsync();
        Task<List<Models.Product.Product>> GetActiveProductsAsync();
        Task<Models.Product.Product> GetProductAsync(int id);
        Task<int> AddAsync(AddProductDTO dto);
        Task<bool> UpdateAsync(int id, UpdateProductDTO dto);
        Task<bool> InactivateAsync(int id, UpdateProductDTO dto);
        Task<bool> DeleteAsync(int id);
    }
}
