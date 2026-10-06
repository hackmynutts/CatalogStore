using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.OrderLine
{
    public class AddOrderLineDTO
    {
        [Display(Name = "Producto")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un producto.")]
        public int ProductID { get; set; }
        [Display(Name = "Cantidad")]
        [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1 000 000.")]
        public int Quantity { get; set; }
        // Porcentaje sobre toda la línea; 99.99 es el mismo tope del CHECK de la base.
        [Display(Name = "Descuento")]
        [Range(0, 99.99, ErrorMessage = "El descuento debe estar entre 0 y 99.99 %.")]
        public decimal Discount { get; set; } = 0.0m;
    }
}
