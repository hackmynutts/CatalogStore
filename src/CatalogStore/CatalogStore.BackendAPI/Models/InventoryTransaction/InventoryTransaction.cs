using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.InventoryTransaction
{
    public enum TransactionType
    {
        None = 0,
        CargaInicial = 1,
        Reduccion= 2,
        ProductoReservado = 3,
        ProductoLiberado = 4,
        IncrementoStock = 5
    }
    [Table("InventoryTransaction_TB")]
    public class InventoryTransaction
    {
        public int InventoryTransactionID { get; set; }
        public int InventoryLineID { get; set; }
        public TransactionType Type { get; set; }
        public int Quantity { get; set; }

    }
}
