using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.OrderLine
{
    public class UpdateOrderLineDTO
    {
        [Display(Name = "Cantidad")]
        [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1 000 000.")]
        public int Quantity { get; set; }
        [Display(Name = "Descuento")]
        [Range(0, 99.99, ErrorMessage = "El descuento debe estar entre 0 y 99.99 %.")]
        public decimal Discount { get; set; } = 0.0m;
    }
}
