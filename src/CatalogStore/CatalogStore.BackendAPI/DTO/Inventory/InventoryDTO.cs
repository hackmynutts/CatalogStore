using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Inventory
{
    public class InventoryDTO
    {
        [Display(Name = "#")]
        public int InventoryID { get; set; }
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Creado Por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name = "Modificado Por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
