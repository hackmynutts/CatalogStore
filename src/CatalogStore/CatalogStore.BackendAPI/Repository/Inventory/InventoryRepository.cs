using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Inventory
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly ApplicationDBContext _context;
        public InventoryRepository(ApplicationDBContext context) => _context = context;
        public async Task<List<Models.Inventory.Inventory>> GetAllInventoriesAsync() => await _context.Inventories.AsNoTracking().ToListAsync();
        public async Task<List<Models.Inventory.Inventory>> GetActiveInventoriesAsync() => await _context.Inventories.AsNoTracking().Where(i => i.StatusID == 1).ToListAsync();
        public async Task<Models.Inventory.Inventory> GetInventoryAsync(int ID) => await _context.Inventories.AsNoTracking().Where(i => i.InventoryID == ID).FirstOrDefaultAsync();
        public async Task<int> AddAsync(Models.Inventory.Inventory inventory)
        {
            await _context.Inventories.AddAsync(inventory);
            await _context.SaveChangesAsync();
            return inventory.InventoryID;
        }
        public async Task<bool> UpdateAsync(Models.Inventory.Inventory inventory)
        {
            _context.Inventories.Update(inventory);
            return await _context.SaveChangesAsync() > 0;
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null) return false;
            _context.Inventories.Remove(inventory);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
