using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Product;
using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Orders;
using CatalogStore.BackendAPI.Services.Product.Cabys;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Product
{
    public class ProductServices : IProductServices
    {
        private const string ModuleName = "Ventas/Productos";
        private const string ProductsTable = "Product";
        private readonly IProductRepository _repository;
        private readonly IOptions<PricingOptions> _pricing;
        private readonly IEventlogServices _eventlogServices;
        private readonly ICabysServices _cabys;
        public const string NotFoundMessage = "Producto no encontrado.";
        private const string CabysUnavailable = "No se pudo consultar el catálogo CABYS de Hacienda. Intentá de nuevo en unos minutos.";
        public ProductServices(IProductRepository repository, IOptions<PricingOptions> pricing, IEventlogServices eventlogServices, ICabysServices cabys)
        {
            _repository = repository;
            _pricing = pricing;
            _eventlogServices = eventlogServices;
            _cabys = cabys;
        }
        public async Task<List<Models.Product.Product>> GetAllProductsAsync() => await _repository.GetAllProductsAsync();
        public async Task<List<Models.Product.Product>> GetActiveProductsAsync() => await _repository.GetActiveProductsAsync();
        public async Task<Models.Product.Product> GetProductAsync(int id) => await _repository.GetProductAsync(id);
        public async Task<Models.Product.Product> GetByProductCode(string code) => await _repository.GetByProductCode(code);
        public async Task<int>AddAsync(AddProductDTO dto)
        {
            Models.Product.Product prod = new Models.Product.Product
            {
                ProductCode = dto.ProductCode,
                ProductName = dto.ProductName,
                ProductDesc = dto.ProductDesc,
                categoria = dto.categoria,
                CostPrice = dto.CostPrice,
                Price = dto.Price,
                PriceCalcIVA = Math.Round((dto.Price ?? 0) * _pricing.Value.IvaRate, 2, MidpointRounding.AwayFromZero),
                UnidadMedida = dto.UnidadMedida,
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
                    existing.CostPrice,
                    existing.Price,
                    existing.PriceCalcIVA,
                    existing.UnidadMedida,
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
                existing.CostPrice = dto.CostPrice;
                existing.Price = dto.Price;
                existing.PriceCalcIVA = Math.Round((dto.Price ?? 0) * _pricing.Value.IvaRate, 2, MidpointRounding.AwayFromZero);
                existing.UnidadMedida = dto.UnidadMedida;
                existing.StatusID = dto.StatusID;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.UtcNow;

                bool updated = await _repository.UpdateAsync(existing);
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
                    existing.UnidadMedida,
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

        public async Task<OperationResult> SetCabysAsync(int id, string? cabysCode, string user)
        {
            cabysCode = string.IsNullOrWhiteSpace(cabysCode) ? null : cabysCode.Trim();
            try
            {
                if (cabysCode is not null)
                {
                    var invalid = await CabysErrorAsync(cabysCode);
                    if (invalid is not null) return OperationResult.Fail(invalid);
                }

                var product = await _repository.GetTrackedAsync(id);
                if (product is null) return OperationResult.Fail(NotFoundMessage);
                if (product.CabysCode == cabysCode) return OperationResult.Ok(id);

                var before = product.CabysCode;
                product.CabysCode = cabysCode;
                product.ModifiedBy = user;
                product.ModifiedOn = DateTime.UtcNow;
                await _repository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, ProductsTable, id.ToString(),
                    cabysCode is null ? "CABYS del producto quitado." : "CABYS del producto asignado.",
                    preData: new { CabysCode = before }, postData: new { CabysCode = cabysCode });
                return OperationResult.Ok(id);
            }
            catch (HttpRequestException) { return OperationResult.Fail(CabysUnavailable); }
            catch (TaskCanceledException) { return OperationResult.Fail(CabysUnavailable); }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return OperationResult.Fail("El producto cambió mientras lo editabas. Recargá la pantalla e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(typeEvent.EditFail, ModuleName, ProductsTable, id.ToString(),
                    "Excepción no controlada al asignar el CABYS.", stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<OperationResult> SetCabysByCategoryAsync(BulkCabysDTO dto, string user)
        {
            try
            {
                var invalid = await CabysErrorAsync(dto.CabysCode);
                if (invalid is not null) return OperationResult.Fail(invalid);

                var products = (await _repository.GetTrackedByCategoryAsync(dto.Category, dto.OnlyEmpty))
                    .Where(p => p.CabysCode != dto.CabysCode).ToList();
                if (products.Count == 0)
                    return OperationResult.Fail(dto.OnlyEmpty
                        ? "No hay productos sin CABYS en esa categoría."
                        : "Todos los productos de esa categoría ya tienen ese CABYS.");

                var now = DateTime.UtcNow;
                foreach (var p in products)
                {
                    p.CabysCode = dto.CabysCode;
                    p.ModifiedBy = user;
                    p.ModifiedOn = now;
                }
                await _repository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, ProductsTable, $"categoria:{(int)dto.Category}",
                    $"CABYS {dto.CabysCode} asignado a {products.Count} productos de la categoría {dto.Category}.",
                    postData: new { dto.Category, dto.CabysCode, dto.OnlyEmpty, Count = products.Count, ProductIds = products.Select(p => p.ProductID) });
                return OperationResult.Ok(products.Count);
            }
            catch (HttpRequestException) { return OperationResult.Fail(CabysUnavailable); }
            catch (TaskCanceledException) { return OperationResult.Fail(CabysUnavailable); }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return OperationResult.Fail("Algún producto cambió mientras se asignaba el CABYS. Recargá la pantalla e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(typeEvent.EditFail, ModuleName, ProductsTable, $"categoria:{(int)dto.Category}",
                    "Excepción no controlada al asignar el CABYS por categoría.", stackTrace: ex.ToString());
                throw;
            }
        }

        // null = el código existe en el catálogo oficial.
        private async Task<string?> CabysErrorAsync(string code)
        {
            if (code.Length != 13 || !code.All(char.IsDigit))
                return "El CABYS tiene 13 dígitos.";
            return await _cabys.GetAsync(code) is null
                ? $"El código {code} no existe en el catálogo CABYS de Hacienda."
                : null;
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
