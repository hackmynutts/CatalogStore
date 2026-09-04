namespace CatalogStore.BackendAPI.Repository.ProductImage
{
    public interface IProductImageRepository
    {
        Task<List<Models.ProductImage.ProductImage>> GetAllProductImagesAsync();
        Task<Models.ProductImage.ProductImage> GetProductImageByIdAsync(int id);
        Task<List<Models.ProductImage.ProductImage>> GetProductImagesByProductIdAsync(int id);
        Task<int> AddAsync(Models.ProductImage.ProductImage productImage);
        Task<bool> UpdateAsync(Models.ProductImage.ProductImage productImage);
        Task<bool> DeleteAsync(int id);
    }
}
