using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Product
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public ProductRepository(ApplicationDBContext applicationDB) { _dbContext = applicationDB; }
        public async Task<List<Models.Product.Product>> GetAllProductsAsync() => await _dbContext.Products.AsNoTracking().Include(p => p.Images).ToListAsync();
        public async Task<List<Models.Product.Product>> GetActiveProductsAsync() => await _dbContext.Products.Where(p=>p.StatusID==1).AsNoTracking().Include(p => p.Images).ToListAsync();
        public async Task<Models.Product.Product> GetProductAsync(int id) => await _dbContext.Products.AsNoTracking().Include(p => p.Images).FirstOrDefaultAsync(p=>p.ProductID==id);
        public async Task<Models.Product.Product> GetByProductCode(string code) => await _dbContext.Products.AsNoTracking().Include(p => p.Images).FirstOrDefaultAsync(p=>p.ProductCode==code);
        public async Task<int> AddAsync(Models.Product.Product prod)
        {
            await _dbContext.Products.AddAsync(prod);
            await _dbContext.SaveChangesAsync();
            return prod.ProductID;                
        }
        public async Task<bool> UpdateAsync(Models.Product.Product prod)
        {
            _dbContext.Products.Update(prod);
            return await _dbContext.SaveChangesAsync() > 0;
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var prod = await _dbContext.Products.FindAsync(id);
            if (prod == null) return false;
            _dbContext.Products.Remove(prod);
            return await _dbContext.SaveChangesAsync() > 0;
        }
        public async Task<Dictionary<int, Models.Product.Product>> GetByIdsAsync(IEnumerable<int> productIds)
        {
            var ids = productIds.Distinct().ToList();
            return await _dbContext.Products
                .AsNoTracking()
                .Where(p => ids.Contains(p.ProductID))
                .ToDictionaryAsync(p => p.ProductID);
        }
        // Con seguimiento a propósito: el servicio modifica las entidades y EF genera solo los UPDATE necesarios.
        public async Task<Dictionary<int, Models.Product.Product>> GetByExternalIdsAsync(IEnumerable<int> externalIds)
        {
            var ids = externalIds.ToList();
            return await _dbContext.Products
                .Where(p => p.ExternalProductID != null && ids.Contains(p.ExternalProductID.Value))
                .ToDictionaryAsync(p => p.ExternalProductID!.Value);
        }
        public async Task AddRangeAsync(IEnumerable<Models.Product.Product> products) => await _dbContext.Products.AddRangeAsync(products);
        public Task<Dictionary<int, int>> GetAvailabilityAsync() =>
            _dbContext.InventoryLines.AsNoTracking()
                .GroupBy(l => l.ProductID)
                .Select(g => new { ProductID = g.Key, Available = g.Sum(l => l.QuantityAvailable) })
                .ToDictionaryAsync(x => x.ProductID, x => Math.Max(x.Available, 0));
        public async Task<List<DTO.Product.PublicCatalogItemDTO>> GetPublicCatalogAsync()
        {
            var products = await _dbContext.Products.AsNoTracking()
                .Where(p => p.StatusID == 1 && p.Price != null)
                .OrderBy(p => p.categoria).ThenBy(p => p.ProductName)
                .Select(p => new
                {
                    p.ProductID, p.ProductCode, p.ProductName, p.categoria,
                    Image = p.Images.OrderBy(i => i.ProductImageID).Select(i => i.Url).FirstOrDefault()
                }).ToListAsync();
            return products.Select(p => new DTO.Product.PublicCatalogItemDTO(
                p.ProductID, p.ProductCode, p.ProductName, (int)p.categoria, p.Image)).ToList();
        }
        public Task<Models.Product.Product?> GetTrackedAsync(int id) =>
            _dbContext.Products.FirstOrDefaultAsync(p => p.ProductID == id);
        public Task<List<Models.Product.Product>> GetTrackedByCategoryAsync(Models.Product.categoryType category, bool onlyWithoutCabys) =>
            _dbContext.Products.Where(p => p.categoria == category && (!onlyWithoutCabys || p.CabysCode == null)).ToListAsync();
        public async Task<int> SaveChangesAsync() => await _dbContext.SaveChangesAsync();
    }
}
