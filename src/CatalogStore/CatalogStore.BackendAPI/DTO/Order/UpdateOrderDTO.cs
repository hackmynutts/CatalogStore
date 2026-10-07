using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
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
