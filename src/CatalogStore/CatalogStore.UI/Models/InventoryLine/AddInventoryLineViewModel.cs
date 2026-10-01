using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.InventoryLine
{
    // Sin cantidades: el stock solo entra por transacciones.
    public class AddInventoryLineViewModel
    {
        public int InventoryID { get; set; }

        [Display(Name = "Producto")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un producto.")]
        public int ProductID { get; set; }

        [Display(Name = "Mínimo para reponer")]
        [Range(0, int.MaxValue, ErrorMessage = "El mínimo debe ser 0 o mayor.")]
        public int QuantityRestock { get; set; }
    }
}
