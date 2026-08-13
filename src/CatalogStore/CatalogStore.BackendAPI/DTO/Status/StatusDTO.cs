using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Status
{
    public class StatusDTO
    {
        [Display(Name = "#")]
        public int StatusID{ get; set; }
        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string name{ get; set; } = string.Empty;
        [Display(Name = "Creado Por")]
        public string CreatedBy{ get; set; } = string.Empty;
        [Display(Name = "Fecha de Creación")]
        public DateTime CreatedOn{ get; set; }
        [Display(Name = "Actualizado Por")]
        public string? UpdatedBy{ get; set; }
        [Display(Name = "Fecha de Actualización")]
        public DateTime? UpdatedOn{ get; set; }
    }
}
