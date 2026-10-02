using CatalogStore.BackendAPI.DTO.InventoryTransactions;

namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    public interface IStockImportServices
    {
        /// <summary>
        /// Carga inicial de existencias desde el inventario del proveedor hacia una bodega.
        /// Con dryRun solo simula: devuelve el resumen sin guardar nada.
        /// </summary>
        Task<StockImportResultDTO> ImportAsync(int inventoryId, bool dryRun, string requestedBy, CancellationToken cancellationToken = default);
    }
}
