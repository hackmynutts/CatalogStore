using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Inventory
{
    public class AddInventoryDTO
    {
        [Display(Name = "Nombre")]
        public string Name { get; set; } = string.Empty;
        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;
        [Display(Name = "Creado Por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
