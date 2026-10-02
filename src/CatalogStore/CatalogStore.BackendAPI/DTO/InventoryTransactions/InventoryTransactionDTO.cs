using CatalogStore.BackendAPI.Models.InventoryTransaction;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryTransactions
{
    public class InventoryTransactionDTO
    {
        [Display(Name = "#")]
        public int InventoryTransactionID { get; set; }
        [Display(Name = "# en Inventario")]
        public int InventoryLineID { get; set; }
        [Display(Name = "Tipo de movimiento")]
        public TransactionType Type { get; set; } = TransactionType.None; 
        [Display(Name = "Nombre del tipo de movimiento")]
        public string TypeName { get; set; } = string.Empty;
       
        [Display(Name = "Cantidad de movimiento")]
        public int TransactionQuantity { get; set; }
        [Display(Name = "Cantidad antes del movimiento")]
        public int QuantityBefore { get; set; }
        [Display(Name = "Cantidad después del movimiento")]
        public int QuantityAfter { get; set; }
        [Display(Name = "Reservado antes del movimiento")]
        public int OnHoldBefore { get; set; }
        [Display(Name = "Reservado después del movimiento")]
        public int OnHoldAfter { get; set; }
        [Display(Name = "Cambio en inventario")]
        public int StockChange { get; set; }
        [Display(Name = "Cambio en espera")]
        public int OnHoldChange { get; set; }
        [Display(Name = "Motivo de referencia")]
        public ReferenceType ReferenceReason { get; set; } = ReferenceType.Ninguna; 
        [Display(Name = "Nombre del motivo de referencia")]
        public string ReferenceReasonName { get; set; } = string.Empty;
        [Display(Name = "ID de referencia")]
        public string? ReferenceID { get; set; }
        [Display(Name = "Razón")]
        public string? Reason { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
