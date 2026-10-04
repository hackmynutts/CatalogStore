using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.InventoryLines
{
    public class InventoryLineRepository : IInventoryLineRepository
    {
        private readonly ApplicationDBContext _context;
        public InventoryLineRepository(ApplicationDBContext context)
        {
            _context = context;
        }
        public async Task<List<Models.InventoryLine.InventoryLine>> GetByInventoryAsync(int inventoryId) =>
            await _context.InventoryLines
                .AsNoTracking()
                .Where(il => il.InventoryID == inventoryId)
                .OrderBy(il => il.Product.ProductName)
                .Include(il => il.Product)
                .ToListAsync();
        public async Task<Models.InventoryLine.InventoryLine?> GetByProductAsync(int productId) =>
            await _context.InventoryLines
                .AsNoTracking()
                .Where(il => il.ProductID == productId)
                .Include(il => il.Product)
                .FirstOrDefaultAsync();
        public async Task<Models.InventoryLine.InventoryLine?> GetByIdAsync(int inventoryLineId) => await _context.InventoryLines.AsNoTracking().Include(il => il.Product).FirstOrDefaultAsync(il => il.InventoryLineID == inventoryLineId);

        public async Task<Models.InventoryLine.InventoryLine?> GetByInventoryAndProductAsync(int inventoryId, int productId) => 
            await _context.InventoryLines.AsNoTracking().FirstOrDefaultAsync(il => il.InventoryID == inventoryId && il.ProductID == productId);
        public async Task<Models.InventoryLine.InventoryLine?> GetForUpdateAsync(int inventoryLineId) => await _context.InventoryLines.FirstOrDefaultAsync(il => il.InventoryLineID == inventoryLineId);
        public async Task<int> AddAsync(Models.InventoryLine.InventoryLine inventoryLine)
        {
            await _context.InventoryLines.AddAsync(inventoryLine);
            await _context.SaveChangesAsync();
            return inventoryLine.InventoryLineID;
        }
        // Con seguimiento: la carga de stock del proveedor modifica estas líneas. Clave = ProductID.
        public async Task<Dictionary<int, Models.InventoryLine.InventoryLine>> GetByInventoryAndProductsAsync(int inventoryId, IEnumerable<int> productIds)
        {
            var ids = productIds.ToList();
            return await _context.InventoryLines
                .Where(il => il.InventoryID == inventoryId && ids.Contains(il.ProductID))
                .ToDictionaryAsync(il => il.ProductID);
        }
        // No guarda: para procesos por lote que guardan todo junto con SaveChangesAsync.
        public void Add(Models.InventoryLine.InventoryLine inventoryLine) => _context.InventoryLines.Add(inventoryLine);
        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
        
    }
}
