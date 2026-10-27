using CatalogStore.BackendAPI.Models.SalesPayment;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Sales
{
    public class AddSalePaymentDTO
    {
        [Display(Name = "Monto")]
        [Range(0.01, 99_999_999.99, ErrorMessage = "El monto debe ser mayor a cero.")]
        public decimal Amount { get; set; }

        // Nullable a propósito: sin [Required] un enum ausente llegaría como 0 y pasaría la validación.
        [Display(Name = "Método de pago")]
        [Required(ErrorMessage = "Seleccione el método de pago.")]
        public PaymentMethod? PaymentMethod { get; set; }

        [Display(Name = "Referencia")]
        [StringLength(75, ErrorMessage = "La referencia no puede exceder los 75 caracteres.")]
        public string? Reference { get; set; }

        // Fecha local (Costa Rica) en que pagó el cliente. Vacía = hoy. No puede ser futura ni anterior a la venta.
        [Display(Name = "Fecha de pago")]
        public DateOnly? PaymentDate { get; set; }

        [Display(Name = "Notas")]
        [StringLength(250, ErrorMessage = "Las notas no pueden exceder los 250 caracteres.")]
        public string? Notes { get; set; }
    }

    public class VoidSalePaymentDTO
    {
        [Display(Name = "Motivo")]
        [Required(ErrorMessage = "Indique el motivo de la anulación.")]
        [StringLength(250, ErrorMessage = "El motivo no puede exceder los 250 caracteres.")]
        public string Reason { get; set; } = string.Empty;
    }

    // Anular una venta completa (solo admin). 250 = largo de SalesHeader.VoidedReason.
    public class VoidSaleDTO
    {
        [Display(Name = "Motivo")]
        [Required(ErrorMessage = "Indique el motivo de la anulación.")]
        [StringLength(250, ErrorMessage = "El motivo no puede exceder los 250 caracteres.")]
        public string Reason { get; set; } = string.Empty;
    }
}
