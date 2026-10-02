namespace CatalogStore.UI.Models.InventoryTransaction
{
    // Copia del enum del backend (Models/InventoryTransaction). Los números deben coincidir siempre:
    // la API los recibe y los devuelve como enteros.
    public enum TransactionType
    {
        None = 0,
        CargaInicial = 1,
        IncrementoStock = 2,
        ProductoReservado = 3,
        ProductoLiberado = 4,
        Venta = 5,
        AjustePositivo = 6,
        AjusteNegativo = 7
    }
}
