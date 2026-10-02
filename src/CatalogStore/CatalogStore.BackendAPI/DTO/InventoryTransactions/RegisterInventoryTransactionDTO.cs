using CatalogStore.BackendAPI.Models.InventoryTransaction;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryTransactions
{
    public class RegisterInventoryTransactionDTO
    {
        [Display(Name = "# en Inventario")]
        [Range(1, int.MaxValue)]
        public int InventoryLineID { get; set; }
        [Display(Name = "Tipo de movimiento")]
        [EnumDataType(typeof(TransactionType), ErrorMessage = "El tipo de movimiento no es válido.")]
        public TransactionType Type { get; set; }
        [Display(Name = "Cantidad de movimiento")]
        [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1 000 000.")]
        public int TransactionQuantity { get; set; }
        [EnumDataType(typeof(ReferenceType), ErrorMessage = "El motivo de referencia no es válido.")]
        public ReferenceType ReferenceReason { get; set; }
        [Display(Name = "ID de referencia")]
        [StringLength(50)]
        public string? ReferenceID { get; set; }
        [Display(Name = "Razón")]
        [StringLength(250, ErrorMessage = "La razón no puede exceder los 250 caracteres.")]
        public string? Reason { get; set; }
    }
}
