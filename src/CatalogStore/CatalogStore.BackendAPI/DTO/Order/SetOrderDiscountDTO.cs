using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Descuento a toda la orden: se escribe en cada línea (reemplaza los individuales). 0 = quitar descuentos.
    public class SetOrderDiscountDTO
    {
        [Display(Name = "Descuento")]
        [Range(0, 99.99, ErrorMessage = "El descuento debe estar entre 0 y 99.99 %.")]
        public decimal Discount { get; set; }
    }
}
