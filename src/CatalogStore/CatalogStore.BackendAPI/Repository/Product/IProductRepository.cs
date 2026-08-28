using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Product
{
    public interface IProductRepository
    {
        Task<List<Models.Product.Product>> GetAllProductsAsync();
        Task<List<Models.Product.Product>> GetActiveProductsAsync();
        Task<Models.Product.Product> GetProductAsync(int id);
        Task<int> AddAsync(Models.Product.Product prod);
        Task<bool> UpdateAsync(Models.Product.Product prod);
        Task<bool> DeleteAsync(int id);
    }
}
