using CatalogStore.BackendAPI.DTO.CatalogExternal;
using CatalogStore.BackendAPI.DTO.InventoryTransactions;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryLine;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Repository.InventoryTransactions;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Inventory;
using CatalogStore.BackendAPI.Services.Product.CatalogExternal;

namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    /// <summary>
    /// Carga inicial de stock desde el inventario del proveedor. Por cada producto del proveedor:
    /// crea la línea en la bodega si no existe y, si el proveedor tiene existencias, registra una CargaInicial.
    /// Las líneas que ya tienen movimientos no se tocan, así que se puede ejecutar varias veces sin duplicar stock.
    /// Trabaja por páginas: cada página se guarda con un solo SaveChanges (líneas + transacciones juntas).
    /// </summary>
    public class StockImportServices : IStockImportServices
    {
        private const string ModuleName = "Administracion/Inventario";
        private const string TableName = "InventoryTransaction";
        private const int DefaultPageSize = 100;
        private const int MaxPageSize = 1000;
        private const int MaxMessages = 200;
        private const int MaxQuantity = 1_000_000;

        // Evita que dos cargas corran a la vez (por ejemplo, un doble clic).
        private static readonly SemaphoreSlim ImportLock = new(1, 1);

        private readonly ICatalogExternalServices _catalogExternal;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
        private readonly IInventoryServices _inventoryServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly IConfiguration _configuration;
        private readonly TimeProvider _time;

        public StockImportServices(
            ICatalogExternalServices catalogExternal,
            IProductRepository productRepository,
            IInventoryLineRepository inventoryLineRepository,
            IInventoryTransactionRepository inventoryTransactionRepository,
            IInventoryServices inventoryServices,
            IEventlogServices eventlogServices,
            IConfiguration configuration,
            TimeProvider time)
        {
            _catalogExternal = catalogExternal;
            _productRepository = productRepository;
            _inventoryLineRepository = inventoryLineRepository;
            _inventoryTransactionRepository = inventoryTransactionRepository;
            _inventoryServices = inventoryServices;
            _eventlogServices = eventlogServices;
            _configuration = configuration;
            _time = time;
        }

        public async Task<StockImportResultDTO> ImportAsync(int inventoryId, bool dryRun, string requestedBy, CancellationToken cancellationToken = default)
        {
            var inventory = await _inventoryServices.GetInventoryAsync(inventoryId);
            if (inventory is null || inventory.StatusID != 1)
                throw new StockImportValidationException("La bodega no existe o está inactiva.");

            if (!await ImportLock.WaitAsync(0, cancellationToken))
                throw new ProductImportInProgressException();

            var result = new StockImportResultDTO { DryRun = dryRun, InventoryID = inventoryId, InventoryName = inventory.Name };
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
                        result.TotalInSource = response.Total;
                    }

                    await ProcessPageAsync(response, inventoryId, dryRun, requestedBy, seen, result);
                    page++;
                }

                if (!dryRun)
                {
                    await _eventlogServices.LogAsync(typeEvent.Import, ModuleName, TableName, inventoryId.ToString(),
                        $"Carga de stock del proveedor en {inventory.Name}: {result.InitialLoads} cargas iniciales ({result.UnitsLoaded} unidades), {result.LinesCreated} líneas nuevas, {result.AlreadyLoaded} ya cargadas, {result.Skipped} omitidas.",
                        postData: result);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (!dryRun)
                {
                    await _eventlogServices.LogAsync(typeEvent.ImportFail, ModuleName, TableName, inventoryId.ToString(),
                        "Falló la carga de stock del proveedor.", postData: result, stackTrace: ex.ToString());
                }
                throw;
            }
            finally
            {
                ImportLock.Release();
            }
        }

        private async Task ProcessPageAsync(ExternalCatalogPageDTO page, int inventoryId, bool dryRun, string requestedBy, HashSet<int> seen, StockImportResultDTO result)
        {
            // 1. Filas válidas del proveedor: ID, código y cantidad entera entre 0 y el máximo.
            var rows = new List<(int ExternalId, string Code, int Quantity)>();
            var skipped = 0;

            foreach (var row in page.Data)
            {
                var externalId = row.Producto.ProductoId;
                var code = row.Producto.CodigoProducto?.Trim() ?? string.Empty;
                var raw = row.Inventario?.TotalDisponible ?? 0;

                if (externalId <= 0)
                {
                    skipped++;
                    AddError(result, $"{code}: el producto no trae ID del proveedor, se omite.");
                    continue;
                }
                // El catálogo puede cambiar mientras se pagina: un producto repetido se procesa una sola vez.
                if (!seen.Add(externalId))
                {
                    skipped++;
                    AddWarning(result, $"[{externalId}] {code}: aparece repetido en la respuesta, se ignora.");
                    continue;
                }
                if (raw < 0 || raw > MaxQuantity || raw != decimal.Truncate(raw))
                {
                    skipped++;
                    AddError(result, $"[{externalId}] {code}: existencia inválida ({raw}), se omite.");
                    continue;
                }

                rows.Add((externalId, code, (int)raw));
            }

            var linesCreated = 0;
            var initialLoads = 0;
            var unitsLoaded = 0;
            var alreadyLoaded = 0;
            var withoutStock = 0;
            var notFound = 0;

            if (rows.Count > 0)
            {
                // 2. Tres consultas por página, no una por producto: productos, líneas de la bodega y cuáles ya tienen movimientos.
                var products = await _productRepository.GetByExternalIdsAsync(rows.Select(r => r.ExternalId));
                var lines = await _inventoryLineRepository.GetByInventoryAndProductsAsync(inventoryId, products.Values.Select(p => p.ProductID));
                var linesWithTransactions = await _inventoryTransactionRepository.GetLineIdsWithTransactionsAsync(lines.Values.Select(l => l.InventoryLineID));

                var now = _time.GetUtcNow().UtcDateTime;
                var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

                foreach (var row in rows)
                {
                    if (!products.TryGetValue(row.ExternalId, out var product))
                    {
                        notFound++;
                        AddWarning(result, $"[{row.ExternalId}] {row.Code}: el producto no existe en el sistema; importá primero el catálogo.");
                        continue;
                    }
                    if (product.StatusID != 1)
                    {
                        skipped++;
                        AddWarning(result, $"[{row.ExternalId}] {row.Code}: el producto está inactivo, se omite.");
                        continue;
                    }

                    lines.TryGetValue(product.ProductID, out var line);
                    if (line is not null && line.StatusID != 1)
                    {
                        skipped++;
                        AddWarning(result, $"[{row.ExternalId}] {row.Code}: la línea está inactiva en esta bodega, se omite.");
                        continue;
                    }
                    if (line is not null && linesWithTransactions.Contains(line.InventoryLineID))
                    {
                        alreadyLoaded++;
                        continue;
                    }

                    if (line is null)
                    {
                        linesCreated++;
                        if (!dryRun)
                        {
                            line = new InventoryLine
                            {
                                InventoryID = inventoryId,
                                ProductID = product.ProductID,
                                Quantity = 0,
                                QuantityOnHold = 0,
                                QuantityRestock = 0,
                                StatusID = 1,
                                CreatedBy = requestedBy,
                                CreatedOn = now
                            };
                            _inventoryLineRepository.Add(line);
                        }
                    }

                    if (row.Quantity == 0)
                    {
                        withoutStock++;
                        continue;
                    }

                    initialLoads++;
                    unitsLoaded += row.Quantity;
                    if (dryRun) continue;

                    // Misma regla que CargaInicial en InventoryTransactionServices: suma al stock, no toca el reservado.
                    int qtyBefore = line!.Quantity;
                    int qtyAfter = qtyBefore + row.Quantity;

                    line.Quantity = qtyAfter;
                    line.LastRestock = today;
                    if (line.InventoryLineID != 0)
                    {
                        line.ModifiedBy = requestedBy;
                        line.ModifiedOn = now;
                    }

                    // Se asigna la navegación y no el ID: en las líneas nuevas el ID todavía no existe;
                    // EF lo completa al guardar las dos cosas en el mismo SaveChanges.
                    _inventoryTransactionRepository.Add(new InventoryTransaction
                    {
                        InventoryLine = line,
                        Type = TransactionType.CargaInicial,
                        TransactionQuantity = row.Quantity,
                        QuantityBefore = qtyBefore,
                        QuantityAfter = qtyAfter,
                        OnHoldBefore = line.QuantityOnHold,
                        OnHoldAfter = line.QuantityOnHold,
                        ReferenceReason = ReferenceType.ImportacionProveedor,
                        ReferenceID = row.ExternalId.ToString(),
                        Reason = "Carga inicial desde el inventario del proveedor.",
                        CreatedBy = requestedBy,
                        CreatedOn = now
                    });
                }

                if (!dryRun)
                    await _inventoryLineRepository.SaveChangesAsync();
            }

            // Los contadores se suman después de guardar: si la página falla, el resumen no cuenta lo que no se guardó.
            result.LinesCreated += linesCreated;
            result.InitialLoads += initialLoads;
            result.UnitsLoaded += unitsLoaded;
            result.AlreadyLoaded += alreadyLoaded;
            result.WithoutStock += withoutStock;
            result.ProductsNotFound += notFound;
            result.Skipped += skipped;
        }

        private static void AddWarning(StockImportResultDTO result, string message)
        {
            result.WarningCount++;
            if (result.Warnings.Count < MaxMessages)
                result.Warnings.Add(message);
        }

        private static void AddError(StockImportResultDTO result, string message)
        {
            result.ErrorCount++;
            if (result.Errors.Count < MaxMessages)
                result.Errors.Add(message);
        }
    }
}
