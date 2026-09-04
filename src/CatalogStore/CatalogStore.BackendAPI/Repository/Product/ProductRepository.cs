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
    }
}
