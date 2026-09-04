using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.ProductImage;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Product;

namespace CatalogStore.BackendAPI.Services.ProductImage
{
    public class ProductImageServices : IProductImageServices
    {
        private readonly IProductImageRepository _productImageRepository;
        private readonly IProductServices _productServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public ProductImageServices(IProductImageRepository productImageRepository, IProductServices productServices, IEventlogServices eventlogServices, IWebHostEnvironment webHostEnvironment)
        {
            _productImageRepository = productImageRepository;
            _productServices = productServices;
            _eventlogServices = eventlogServices;
            _webHostEnvironment = webHostEnvironment;
        }
        public async Task<List<Models.ProductImage.ProductImage>> GetAllProductImagesAsync() => await _productImageRepository.GetAllProductImagesAsync();
        public async Task<Models.ProductImage.ProductImage> GetProductImageByIdAsync(int id) => await _productImageRepository.GetProductImageByIdAsync(id);
        public async Task<List<Models.ProductImage.ProductImage>> GetProductImagesByProductIdAsync(int id) => await _productImageRepository.GetProductImagesByProductIdAsync(id);
        public async Task<int> UploadAsync(int productId, IFormFile image, string CreatedBy)
        {
            try
            {
                if (image.Length > 0) 
                {
                    string productFolder = productId.ToString();
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products", productFolder);
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    string extension = Path.GetExtension(image.FileName);
                    if (extension != null && !(extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)))
                        return 0;

                    string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                    string completeFilePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using var stream = new FileStream(completeFilePath, FileMode.Create);
                    await image.CopyToAsync(stream);

                    Models.ProductImage.ProductImage entity = new Models.ProductImage.ProductImage {
                                                                                                        ProductID = productId,
                                                                                                        Url = $"/images/products/{productFolder}/{uniqueFileName}",
                                                                                                        ContentType = image.ContentType,
                                                                                                        CreatedBy = CreatedBy,
                                                                                                        CreatedOn = DateTime.Now
                                                                                                    };
                    int res = await _productImageRepository.AddAsync(entity);
                    await _eventlogServices.LogAsync(
                                                        typeEvent.Add,
                                                        "Ventas/Productos",
                                                        "ProductImages",
                                                        res.ToString(),
                                                        "Imagen del Producto creada.",
                                                        postData: entity);
                    return res;
                }
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.AddFail,
                    "Ventas/Productos",
                    "ProductImages",
                    0.ToString(),
                    "Excepción no controlada al una imagen al producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var before = await _productImageRepository.GetProductImageByIdAsync(id);
                if (before == null) return false;

                bool deleted = await _productImageRepository.DeleteAsync(id);
                if (!deleted)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.DeleteFail,
                        "Ventas/Productos",
                        "ProductImages",
                        id.ToString(),
                        "No se pudo eliminar la imagen del producto (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                File.Delete(Path.Combine(_webHostEnvironment.WebRootPath, before.Url.TrimStart('/')));
                await _eventlogServices.LogAsync(
                    typeEvent.Delete,
                    "Ventas/Productos",
                    "ProductImages",
                    id.ToString(),
                    "Imagen del producto eliminada.",
                    preData: before);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    "Ventas/Productos",
                    "ProductImages",
                    id.ToString(),
                    "Excepción no controlada al eliminar una imagen del producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

    }
}
