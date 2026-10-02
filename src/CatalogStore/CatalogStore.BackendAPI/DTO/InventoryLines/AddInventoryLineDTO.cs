using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryLines
{
    public class AddInventoryLineDTO
    {
        [Display(Name = "Inventario")]
        public int InventoryID { get; set; }
        [Display(Name = "Producto")]
        public int ProductID { get; set; }
        [Display(Name = "Cantidad limite")]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad de reabastecimiento debe ser un número entero positivo.")]
        public int QuantityRestock { get; set; } 
        [Display(Name = "Creado Por")]
        public string CreatedBy { get; set; } = string.Empty;
    }
}
