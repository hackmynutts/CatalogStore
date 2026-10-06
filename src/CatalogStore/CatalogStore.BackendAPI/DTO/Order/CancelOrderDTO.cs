using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Cancelar libera el stock reservado. El motivo no tiene columna propia: queda en la bitácora.
    public class CancelOrderDTO
    {
        [Display(Name = "Motivo")]
        [Required(ErrorMessage = "Indique el motivo de la cancelación.")]
        [StringLength(250, ErrorMessage = "El motivo no puede exceder los 250 caracteres.")]
        public string Reason { get; set; } = string.Empty;
    }
}
