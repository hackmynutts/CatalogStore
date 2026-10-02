namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    // Error de datos de entrada (por ejemplo, bodega inexistente o inactiva). El controller lo devuelve como 400,
    // a diferencia de los errores del proveedor (502/504).
    public class StockImportValidationException : Exception
    {
        public StockImportValidationException(string message) : base(message) { }
    }
}
