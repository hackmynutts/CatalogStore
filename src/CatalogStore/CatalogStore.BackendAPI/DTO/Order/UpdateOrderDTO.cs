using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Solo los datos de cabecera. El estado cambia únicamente por start / complete / cancel (cada uno mueve stock),
    // el total se recalcula con las líneas, y el usuario y la fecha de modificación los pone el servicio.
    public class UpdateOrderDTO
    {
        [Display(Name = "Cliente")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
        public int ClientID { get; set; }
        [Display(Name = "Notas")]
        [StringLength(500, ErrorMessage = "Las notas no pueden exceder los 500 caracteres.")]
        public string? Notes { get; set; }
    }
}
