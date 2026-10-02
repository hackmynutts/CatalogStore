using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryTransactions;

namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    public interface IInventoryTransactionServices
    {
        Task<List<InventoryTransactionDTO>> GetByLineAsync(int inventoryLineId);
        Task<OperationResult> RegisterAsync(RegisterInventoryTransactionDTO dto, string createdBy);
    }
}
