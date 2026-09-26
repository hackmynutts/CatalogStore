using CatalogStore.BackendAPI.DTO.CatalogExternal;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public class ProductImportServices : IProductImportServices
    {
        private const string ModuleName = "Ventas/Productos";
        private const string ProductsTable = "Product";
        private const int DefaultPageSize = 100;
        private const int MaxPageSize = 1000;
        private const int MaxMessages = 200;

        // Evita que dos importaciones corran a la vez (por ejemplo, un doble clic).
        private static readonly SemaphoreSlim ImportLock = new(1, 1);

        private readonly ICatalogExternalServices _catalogExternal;
        private readonly IProductRepository _repository;
        private readonly IEventlogServices _eventlogServices;
        private readonly IConfiguration _configuration;

        public ProductImportServices(
            ICatalogExternalServices catalogExternal,
            IProductRepository repository,
            IEventlogServices eventlogServices,
            IConfiguration configuration)
        {
            _catalogExternal = catalogExternal;
            _repository = repository;
            _eventlogServices = eventlogServices;
            _configuration = configuration;
        }

        public async Task<ProductImportResultDTO> ImportAsync(bool dryRun, string requestedBy, CancellationToken cancellationToken = default)
        {
            if (!await ImportLock.WaitAsync(0, cancellationToken))
                throw new ProductImportInProgressException();

            var result = new ProductImportResultDTO { DryRun = dryRun };
            try
            {
                var pageSize = Math.Clamp(_configuration.GetValue<int?>("ExternalCatalog:PageSize") ?? DefaultPageSize, 1, MaxPageSize);
                var seen = new HashSet<int>();
                var page = 1;
                var totalPages = 1;

                while (page <= totalPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var response = await _catalogExternal.GetProductsPageAsync(page, pageSize, cancellationToken);
                    if (page == 1)
                    {
                        if (response.TotalPages <= 0 && response.Total > 0)
                            throw new InvalidOperationException("El catálogo externo no informó el total de páginas.");

                        totalPages = Math.Max(response.TotalPages, 1);
                        result.TotalPages = totalPages;
                        result.TotalInSource = response.Total;
                    }

                    await ProcessPageAsync(response, dryRun, requestedBy, seen, result);
                    page++;
                }

                if (!dryRun)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Import,
                        ModuleName,
                        ProductsTable,
                        "import",
                        $"Importación de catálogo externo: {result.Created} creados, {result.Updated} actualizados, {result.Unchanged} sin cambios, {result.Skipped} omitidos.",
                        postData: result);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (!dryRun)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.ImportFail,
                        ModuleName,
                        ProductsTable,
                        "import",
                        "Falló la importación de catálogo externo.",
                        postData: result,
                        stackTrace: ex.ToString());
                }
                throw;
            }
            finally
            {
                ImportLock.Release();
            }
        }

        private async Task ProcessPageAsync(ExternalCatalogPageDTO page, bool dryRun, string requestedBy, HashSet<int> seen, ProductImportResultDTO result)
        {
            var valid = new List<ProductImportItem>();
            var skipped = 0;

            foreach (var row in page.Data)
            {
                var item = ExternalProductMapper.Map(row.Producto);
                foreach (var warning in item.Warnings)
                    AddWarning(result, warning);

                if (!item.IsValid)
                {
                    skipped++;
                    foreach (var error in item.Errors)
                        AddError(result, error);
                    continue;
                }

                // El catálogo puede cambiar mientras se pagina: un producto repetido se procesa una sola vez.
                if (!seen.Add(item.ExternalProductID))
                {
                    skipped++;
                    AddWarning(result, $"[{item.ExternalProductID}] {item.ProductCode}: aparece repetido en la respuesta, se ignora.");
                    continue;
                }

                valid.Add(item);
            }

            var created = 0;
            var updated = 0;
            var unchanged = 0;

            if (valid.Count > 0)
            {
                var existing = await _repository.GetByExternalIdsAsync(valid.Select(i => i.ExternalProductID));
                var toAdd = new List<Models.Product.Product>();
                var now = DateTime.UtcNow;

                foreach (var item in valid)
                {
                    if (existing.TryGetValue(item.ExternalProductID, out var product))
                    {
                        if (ApplyChanges(product, item, requestedBy, now, apply: !dryRun))
                            updated++;
                        else
                            unchanged++;
                    }
                    else
                    {
                        created++;
                        if (!dryRun)
                            toAdd.Add(NewProduct(item, requestedBy, now));
                    }
                }

                if (!dryRun)
                {
                    if (toAdd.Count > 0)
                        await _repository.AddRangeAsync(toAdd);
                    await _repository.SaveChangesAsync();
                }
            }

            // Los contadores se suman después de guardar: si la página falla, el resumen no cuenta lo que no se guardó.
            result.Created += created;
            result.Updated += updated;
            result.Unchanged += unchanged;
            result.Skipped += skipped;
        }

        /// <summary>
        /// Compara un producto existente con el importado. La API pisa código, nombre, descripción y precios,
        /// y solo puede desactivar (nunca reactivar). Categoría, unidad e imágenes no se tocan.
        /// Devuelve true si hay diferencias; solo modifica la entidad cuando apply es true.
        /// </summary>
        private static bool ApplyChanges(Models.Product.Product product, ProductImportItem item, string requestedBy, DateTime now, bool apply)
        {
            var deactivate = item.StatusID == 2 && product.StatusID != 2;
            var changed = product.ProductCode != item.ProductCode
                || product.ProductName != item.ProductName
                || product.ProductDesc != item.ProductDesc
                || product.Price != item.Price
                || product.PriceCalcIVA != item.PriceCalcIVA
                || deactivate;

            if (changed && apply)
            {
                product.ProductCode = item.ProductCode;
                product.ProductName = item.ProductName;
                product.ProductDesc = item.ProductDesc;
                product.Price = item.Price;
                product.PriceCalcIVA = item.PriceCalcIVA;
                if (deactivate)
                    product.StatusID = 2;
                product.ModifiedBy = requestedBy;
                product.ModifiedOn = now;
            }

            return changed;
        }

        private static Models.Product.Product NewProduct(ProductImportItem item, string requestedBy, DateTime now) => new()
        {
            ExternalProductID = item.ExternalProductID,
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            ProductDesc = item.ProductDesc,
            Price = item.Price,
            PriceCalcIVA = item.PriceCalcIVA,
            UnidadMedida = item.UnidadMedida,
            StatusID = item.StatusID,
            CreatedBy = requestedBy,
            CreatedOn = now
        };

        private static void AddWarning(ProductImportResultDTO result, string message)
        {
            result.WarningCount++;
            if (result.Warnings.Count < MaxMessages)
                result.Warnings.Add(message);
        }

        private static void AddError(ProductImportResultDTO result, string message)
        {
            result.ErrorCount++;
            if (result.Errors.Count < MaxMessages)
                result.Errors.Add(message);
        }
    }
}
