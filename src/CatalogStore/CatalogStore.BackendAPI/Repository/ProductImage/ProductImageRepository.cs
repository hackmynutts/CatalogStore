using CatalogStore.BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.ProductImage
{
    public class ProductImageRepository : IProductImageRepository
    {
        private readonly ApplicationDBContext _context;
        public ProductImageRepository(ApplicationDBContext context) { _context = context; }
        public async Task<List<Models.ProductImage.ProductImage>> GetAllProductImagesAsync() => await _context.ProductImages.AsNoTracking().ToListAsync();
        public async Task<Models.ProductImage.ProductImage> GetProductImageByIdAsync(int id) => await _context.ProductImages.AsNoTracking().FirstOrDefaultAsync(pi => pi.ProductImageID == id);
        public async Task<List<Models.ProductImage.ProductImage>> GetProductImagesByProductIdAsync(int id) => await _context.ProductImages.Where(pi => pi.ProductID == id).AsNoTracking().OrderBy(pi => pi.ProductImageID).ToListAsync();
        public async Task<int> AddAsync(Models.ProductImage.ProductImage productImage)
        {
            _context.ProductImages.Add(productImage);
            await _context.SaveChangesAsync();
            return productImage.ProductImageID;
        }
        public async Task<bool> UpdateAsync(Models.ProductImage.ProductImage productImage)
        {
            _context.ProductImages.Update(productImage);            
            return await _context.SaveChangesAsync()>0;
        }
        public async Task<bool> DeleteAsync(int id)
        {
            var productImage = await _context.ProductImages.FindAsync(id);
            if (productImage == null) return false;
            _context.ProductImages.Remove(productImage);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
