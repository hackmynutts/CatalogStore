using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.ProductImage
{
    public class UpdateProductImageDTO
    {
        [Display(Name = "Producto")]
        public int ProductID { get; set; }
        [Required]
        [Display(Name = "Url")]
        public string Url { get; set; } = string.Empty;
        [Display(Name = "Tipo de contenido")]
        public string? ContentType { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
