using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.InventoryTransaction
{
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
    public enum ReferenceReason
    {
        Ninguna = 0,
        ImportacionProveedor= 1,
        Orden = 2,
        AjusteManual = 3
    }
    [Table("InventoryTransaction_TB")]
    public class InventoryTransaction
    {
        public int InventoryTransactionID { get; set; }
        public int InventoryLineID { get; set; }
        public TransactionType Type { get; set; }
        public int Quantity { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public int OnHoldBefore { get; set; }
        public int OnHoldAfter { get; set; }
        public ReferenceReason ReferenceReason { get; set; }
        public string? Reference { get; set; } 
        public string? Reason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public Models.InventoryLine.InventoryLine InventoryLine { get; set; } = null!;
    }
}
