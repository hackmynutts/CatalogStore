using CatalogStore.BackendAPI.DTO.OrderLine;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Número, estado, totales, usuario y fecha los pone el servicio; el cliente solo elige el cliente y los productos.
    public class AddOrderDTO
    {
        [Display(Name = "Cliente")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
        public int ClientID { get; set; }
        [Display(Name = "Notas")]
        [StringLength(500, ErrorMessage = "Las notas no pueden exceder los 500 caracteres.")]
        public string? Notes { get; set; }
        // [ApiController] también valida los atributos de cada línea de la lista.
        [Display(Name = "Productos")]
        [Required(ErrorMessage = "La orden debe tener al menos un producto.")]
        [MinLength(1, ErrorMessage = "La orden debe tener al menos un producto.")]
        [MaxLength(200, ErrorMessage = "Una orden no puede tener más de 200 productos.")]
        public List<AddOrderLineDTO> Lines { get; set; } = new();
    }
}
