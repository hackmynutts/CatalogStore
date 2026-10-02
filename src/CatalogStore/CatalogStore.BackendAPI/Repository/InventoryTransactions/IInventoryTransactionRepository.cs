using CatalogStore.BackendAPI.Models.InventoryTransaction;

namespace CatalogStore.BackendAPI.Repository.InventoryTransactions
{
    public interface IInventoryTransactionRepository
    {
        Task<List<InventoryTransaction>> GetAllAsync();
        Task<List<InventoryTransaction>> GetByLineAsync(int inventoryLineId);
        void Add(InventoryTransaction inventoryTransaction);
        Task<bool> HasTransactionsAsync(int inventoryLineId);
        Task<HashSet<int>> GetLineIdsWithTransactionsAsync(IEnumerable<int> inventoryLineIds);
    }
}
