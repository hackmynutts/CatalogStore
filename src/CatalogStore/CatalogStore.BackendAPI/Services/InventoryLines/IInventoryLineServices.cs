using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryLines;

namespace CatalogStore.BackendAPI.Services.InventoryLines
{
    public interface IInventoryLineServices
    {
        Task<List<InventoryLineDTO>> GetByInventoryAsync(int inventoryID);
        Task<InventoryLineDTO?> GetByIdAsync(int inventoryLineID);
        Task<OperationResult> AddAsync(AddInventoryLineDTO dto);
        Task<OperationResult> UpdateAsync(UpdateInventoryLineDTO dto);
        Task<OperationResult> InactivateAsync(int inventoryLineID, string modifiedBy);
    }
}