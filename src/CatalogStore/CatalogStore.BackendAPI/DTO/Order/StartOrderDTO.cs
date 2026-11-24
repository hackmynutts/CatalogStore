using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Cuerpo opcional de "iniciar orden": las notas se guardan solo si la orden se crea nueva
    // (si el cliente ya tenía una en proceso, se abre esa tal cual).
    public class StartOrderDTO
    {
        [Display(Name = "Notas")]
        [StringLength(500, ErrorMessage = "Las notas no pueden exceder los 500 caracteres.")]
        public string? Notes { get; set; }
        // Correo del vendedor a cuyo nombre queda la orden. Solo Admin/AdminIT lo pueden elegir; sin valor, la orden queda a nombre de quien la crea.
        public string? Seller { get; set; }
    }
}
