using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.InventoryTransaction
{
    // Movimiento manual. Sin ReferenceReason: la API lo fija como ajuste manual.
    public class RegisterInventoryTransactionViewModel
    {
        public int InventoryLineID { get; set; }

        [Display(Name = "Tipo de movimiento")]
        public TransactionType Type { get; set; } = TransactionType.IncrementoStock;

        [Display(Name = "Cantidad")]
        [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1 000 000.")]
        public int TransactionQuantity { get; set; }

        [Display(Name = "Referencia")]
        [StringLength(50, ErrorMessage = "La referencia no puede exceder los 50 caracteres.")]
        public string? ReferenceID { get; set; }

        [Display(Name = "Razón")]
        [StringLength(250, ErrorMessage = "La razón no puede exceder los 250 caracteres.")]
        public string? Reason { get; set; }
    }
}
