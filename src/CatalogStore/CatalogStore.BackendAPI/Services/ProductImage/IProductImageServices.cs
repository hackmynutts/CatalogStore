namespace CatalogStore.BackendAPI.Services.ProductImage
{
    public interface IProductImageServices
    {
        Task<List<Models.ProductImage.ProductImage>> GetAllProductImagesAsync();
        Task<Models.ProductImage.ProductImage> GetProductImageByIdAsync(int id);
        Task<List<Models.ProductImage.ProductImage>> GetProductImagesByProductIdAsync(int id);
        Task<int> UploadAsync(int productId, IFormFile image, string CreatedBy);
        Task<bool> DeleteAsync(int id);
    }
}
