namespace CatalogStore.BackendAPI.Repository.Inventory
{
    public interface IInventoryRepository
    {
        Task<List<Models.Inventory.Inventory>> GetAllInventoriesAsync();
        Task<List<Models.Inventory.Inventory>> GetActiveInventoriesAsync();
        Task<Models.Inventory.Inventory> GetInventoryAsync(int ID);
        Task<int> AddAsync(Models.Inventory.Inventory inventory);
        Task<bool> UpdateAsync(Models.Inventory.Inventory inventory);
        Task<bool> DeleteAsync(int id);
    }
}
