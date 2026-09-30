using CatalogStore.BackendAPI.Models.InventoryLine;

namespace CatalogStore.BackendAPI.Repository.InventoryLines
{
    public interface IInventoryLineRepository
    {
        Task<List<InventoryLine>> GetByInventoryAsync(int inventoryId);
        Task<InventoryLine?> GetByIdAsync(int inventoryLineId);
        Task<InventoryLine?> GetByInventoryAndProductAsync(int inventoryId, int productId);
        Task<int> AddAsync(InventoryLine inventoryLine);
        Task<bool> UpdateAsync(int inventoryLineId, int quantityRestock, int statusId, string modifiedBy, DateTime modifiedOn);
        Task<InventoryLine?> GetForUpdateAsync(int inventoryLineId);
        Task<int> SaveChangesAsync();
        Task<bool> InactivateAsync(int inventoryLineId, string modifiedBy, DateTime modifiedOn);
    }
}
