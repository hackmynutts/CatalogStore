using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.ProductImage
{
    public class AddProductImageDTO
    {
        [Display(Name = "Producto")]
        public int ProductID { get; set; }
        [Required]
        [Display(Name = "Url")]
        public string Url { get; set; } = string.Empty;
        [Display(Name = "Tipo de contenido")]
        public string? ContentType { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
