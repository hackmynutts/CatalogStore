using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.Product
{
    // Paso 2 del alta de un producto: su línea en la bodega y la carga inicial de stock.
    public class AddToInventoryViewModel
    {
        public int ProductID { get; set; }
        public int InventoryID { get; set; }

        [Display(Name = "Cantidad inicial")]
        [Range(0, 1_000_000, ErrorMessage = "La cantidad inicial debe estar entre 0 y 1 000 000.")]
        public int InitialQuantity { get; set; }

        [Display(Name = "Mínimo para reponer")]
        [Range(0, 1_000_000, ErrorMessage = "El mínimo debe estar entre 0 y 1 000 000.")]
        public int QuantityRestock { get; set; }
    }
}
