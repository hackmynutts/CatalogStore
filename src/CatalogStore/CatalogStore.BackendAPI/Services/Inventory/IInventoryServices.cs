using CatalogStore.BackendAPI.DTO.Inventory;

namespace CatalogStore.BackendAPI.Services.Inventory
{
    public interface IInventoryServices
    {
        Task<List<Models.Inventory.Inventory>> GetAllInventoriesAsync();
        Task<List<Models.Inventory.Inventory>> GetActiveInventoriesAsync();
        Task<Models.Inventory.Inventory> GetInventoryAsync(int ID);
        Task<int> AddAsync(AddInventoryDTO inventory);
        Task<bool> UpdateAsync(UpdateInventoryDTO dto);
        Task<bool> InactivateAsync(UpdateInventoryDTO dto);
        Task<bool> DeleteAsync(int id);
    }
}
