using CatalogStore.BackendAPI.DTO.Product;
using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Product
{
    public class ProductServices : IProductServices
    {
        private const string ModuleName = "Ventas/Productos";
        private const string ProductsTable = "Product";
        private readonly IProductRepository _repository;
        private readonly IEventlogServices _eventlogServices;
        public ProductServices(IProductRepository repository, IEventlogServices eventlogServices)
        {
            _repository = repository;
            _eventlogServices = eventlogServices;
        }
        public async Task<List<Models.Product.Product>> GetAllProductsAsync() => await _repository.GetAllProductsAsync();
        public async Task<List<Models.Product.Product>> GetActiveProductsAsync() => await _repository.GetActiveProductsAsync();
        public async Task<Models.Product.Product> GetProductAsync(int id) => await _repository.GetProductAsync(id);
        public async Task<int>AddAsync(AddProductDTO dto)
        {
            Models.Product.Product prod = new Models.Product.Product
            {
                ProductCode = dto.ProductCode,
                ProductName = dto.ProductName,
                ProductDesc = dto.ProductDesc,
                categoria = dto.categoria,
                Price = dto.Price,
                StatusID = 1,
                CreatedBy = dto.CreatedBy,
                CreatedOn = DateTime.UtcNow
            };
            try
            {
                int id = await _repository.AddAsync(prod);
                await _eventlogServices.LogAsync(
                    typeEvent.Add,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Producto creado.",
                    postData: prod);

                return id;
            }
            catch (Exception ex)
            {
                   await _eventlogServices.LogAsync(
                    typeEvent.AddFail,
                    ModuleName,
                    ProductsTable,
                    prod.ProductName,
                    "Excepción no controlada al crear un producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> UpdateAsync(int id, UpdateProductDTO dto)
        {
            try
            {
                var existing = await _repository.GetProductAsync(id);
                if (existing == null) return false;

                var before = new
                {
                    existing.ProductID,
                    existing.ProductCode,
                    existing.ProductName,
                    existing.ProductDesc,
                    existing.categoria,
                    existing.Price,
                    existing.StatusID,
                    existing.CreatedBy,
                    existing.CreatedOn,
                    existing.ModifiedBy,
                    existing.ModifiedOn
                };

                existing.ProductCode = dto.ProductCode;
                existing.ProductName = dto.ProductName;
                existing.ProductDesc = dto.ProductDesc;
                existing.categoria = dto.categoria;
                existing.Price = dto.Price;
                existing.StatusID = dto.StatusID;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.UtcNow;

                var updated = await _repository.UpdateAsync(existing);
                if (!updated)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        ModuleName,
                        ProductsTable,
                        id.ToString(),
                        "No se pudo actualizar el producto (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Edit,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Producto actualizado.",
                    preData: before,
                    postData: existing);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Excepción no controlada al editar un producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> InactivateAsync(int id, UpdateProductDTO dto)
        {
            try
            {
                var existing = await _repository.GetProductAsync(id);
                if (existing == null) return false;

                var before = new
                {
                    existing.ProductID,
                    existing.ProductCode,
                    existing.ProductName,
                    existing.ProductDesc,
                    existing.categoria,
                    existing.Price,
                    existing.StatusID,
                    existing.CreatedBy,
                    existing.CreatedOn,
                    existing.ModifiedBy,
                    existing.ModifiedOn
                };

                existing.StatusID = 2;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.UtcNow;

                var updated = await _repository.UpdateAsync(existing);
                if (!updated)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.InactivateFail,
                        ModuleName,
                        ProductsTable,
                        id.ToString(),
                        "No se pudo inactivar el producto (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Inactivate,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Producto inactivado.",
                    preData: before,
                    postData: existing);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.InactivateFail,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Excepción no controlada al inactivar un producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var before = await _repository.GetProductAsync(id);
                if (before == null) return false;

                var deleted = await _repository.DeleteAsync(id);
                if (!deleted)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.DeleteFail,
                        ModuleName,
                        ProductsTable,
                        id.ToString(),
                        "No se pudo eliminar el producto (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Delete,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Producto eliminado.",
                    preData: before);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    ModuleName,
                    ProductsTable,
                    id.ToString(),
                    "Excepción no controlada al eliminar un producto.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
    }
}
