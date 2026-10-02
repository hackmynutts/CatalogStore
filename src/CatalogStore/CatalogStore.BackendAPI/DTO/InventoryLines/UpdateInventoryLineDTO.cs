using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryLines
{
    public class UpdateInventoryLineDTO
    {
        [Display(Name = "#")]
        public int InventoryLineID { get; set; }
        [Display(Name = "Cantidad limite")]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad de reabastecimiento debe ser un número entero positivo.")]
        public int QuantityRestock { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Modificado Por")]
        public string? ModifiedBy { get; set; }
    }
}
