using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryLines
{
    public class UpdateInventoryLineDTO
    {
        [Display(Name = "#")]
        public int InventoryLineID { get; set; }
        [Display(Name = "Cantidad limite")]
        public int QuantityRestock { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Modificado Por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
