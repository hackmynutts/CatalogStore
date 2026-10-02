using CatalogStore.BackendAPI.Models.InventoryLine;

namespace CatalogStore.BackendAPI.Repository.InventoryLines
{
    public interface IInventoryLineRepository
    {
        Task<List<InventoryLine>> GetByInventoryAsync(int inventoryId);
        Task<InventoryLine?> GetByIdAsync(int inventoryLineId);
        Task<InventoryLine?> GetByInventoryAndProductAsync(int inventoryId, int productId);
        Task<Dictionary<int, InventoryLine>> GetByInventoryAndProductsAsync(int inventoryId, IEnumerable<int> productIds);
        Task<int> AddAsync(InventoryLine inventoryLine);
        void Add(InventoryLine inventoryLine);
        Task<InventoryLine?> GetForUpdateAsync(int inventoryLineId);
        Task<int> SaveChangesAsync();
    }
}
