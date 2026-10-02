using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.InventoryTransactions
{
    public class InventoryTransactionRepository : IInventoryTransactionRepository
    {
        private readonly ApplicationDBContext _context;
        public InventoryTransactionRepository(ApplicationDBContext context)
        {
            _context = context;
        }
        public async Task<List<InventoryTransaction>> GetAllAsync() => await _context.InventoryTransactions.AsNoTracking().Include(it => it.InventoryLine).ToListAsync();
        public async Task<List<InventoryTransaction>> GetByLineAsync(int inventoryLineId) => 
                await _context.InventoryTransactions.AsNoTracking().Where(it => it.InventoryLineID == inventoryLineId).OrderByDescending(it => it.CreatedOn).ThenByDescending(it => it.InventoryTransactionID).ToListAsync();
        public void Add(InventoryTransaction inventoryTransaction) => _context.InventoryTransactions.Add(inventoryTransaction);
        public async Task<bool> HasTransactionsAsync(int inventoryLineId) =>
                await _context.InventoryTransactions.AnyAsync(it => it.InventoryLineID == inventoryLineId);
        // Versión por lote de HasTransactionsAsync: una sola consulta para toda una página de la carga de stock.
        public async Task<HashSet<int>> GetLineIdsWithTransactionsAsync(IEnumerable<int> inventoryLineIds)
        {
            var ids = inventoryLineIds.ToList();
            if (ids.Count == 0) return new HashSet<int>();
            var withTransactions = await _context.InventoryTransactions
                .Where(it => ids.Contains(it.InventoryLineID))
                .Select(it => it.InventoryLineID)
                .Distinct()
                .ToListAsync();
            return withTransactions.ToHashSet();
        }
    }
}
