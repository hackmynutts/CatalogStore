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
        public async Task<bool> UpdateAsync(int inventoryLineId,int quantityRestock, int statusId, string modifiedBy, DateTime modifiedOn)
        {
            Models.InventoryLine.InventoryLine inventoryLine = await _context.InventoryLines.FindAsync(inventoryLineId);
            if (inventoryLine == null) return false;

            inventoryLine.QuantityRestock = quantityRestock;
            inventoryLine.StatusID = statusId;
            inventoryLine.ModifiedBy = modifiedBy;
            inventoryLine.ModifiedOn = modifiedOn;
            return await _context.SaveChangesAsync() > 0;
        }
        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
        public async Task<bool> InactivateAsync (int inventoryLineId, string modifiedBy, DateTime modifiedOn)
        {
            Models.InventoryLine.InventoryLine inventoryLine = await _context.InventoryLines.FindAsync(inventoryLineId);
            if (inventoryLine == null) return false;

            inventoryLine.StatusID = 2;
            inventoryLine.ModifiedBy = modifiedBy;
            inventoryLine.ModifiedOn = modifiedOn;
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
