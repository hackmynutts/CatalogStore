namespace CatalogStore.UI.Models.InventoryTransaction
{
    // Un movimiento del historial de una línea. TypeName y ReferenceReasonName ya vienen en español desde la API.
    public class InventoryTransactionViewModel
    {
        public int InventoryTransactionID { get; set; }
        public int InventoryLineID { get; set; }
        public TransactionType Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int TransactionQuantity { get; set; }
        public int QuantityBefore { get; set; }
        public int QuantityAfter { get; set; }
        public int OnHoldBefore { get; set; }
        public int OnHoldAfter { get; set; }
        public int StockChange { get; set; }
        public int OnHoldChange { get; set; }
        public int ReferenceReason { get; set; }
        public string ReferenceReasonName { get; set; } = string.Empty;
        public string? ReferenceID { get; set; }
        public string? Reason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
