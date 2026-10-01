using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.InventoryLine
{
    // Sin cantidades: el stock solo cambia por transacciones.
    public class UpdateInventoryLineViewModel
    {
        public int InventoryLineID { get; set; }

        [Display(Name = "Mínimo para reponer")]
        [Range(0, int.MaxValue, ErrorMessage = "El mínimo debe ser 0 o mayor.")]
        public int QuantityRestock { get; set; }

        [Display(Name = "Estado")]
        public int StatusID { get; set; }
    }
}
