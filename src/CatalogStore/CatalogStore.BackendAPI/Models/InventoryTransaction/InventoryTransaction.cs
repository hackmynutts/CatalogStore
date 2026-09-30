using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.InventoryTransaction
{
    public enum TransactionType
    {
        None = 0,
        CargaInicial = 1,
        [Display(Name = "Incremento de Stock")]
        IncrementoStock = 2,
        ProductoReservado = 3,
        ProductoLiberado = 4,
        Venta = 5,
        AjustePositivo = 6,
        AjusteNegativo = 7
    }
    public enum ReferenceType
    {
        Ninguna = 0,
        [Display(Name = "Importación de Proveedor")]
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
        public int TransactionQuantity { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public int OnHoldBefore { get; set; }
        public int OnHoldAfter { get; set; }
        public ReferenceType ReferenceReason { get; set; }
        public string? ReferenceID { get; set; } 
        public string? Reason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public Models.InventoryLine.InventoryLine InventoryLine { get; set; } = null!;
    }
}
