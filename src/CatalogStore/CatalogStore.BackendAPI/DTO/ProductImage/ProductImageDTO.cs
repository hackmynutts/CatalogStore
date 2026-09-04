using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.ProductImage
{
    public class ProductImageDTO
    {
        [Display(Name ="#")]
        public int ProductImageID { get; set; }
        [Display(Name ="Producto")]
        public int ProductID { get; set; }
        [Required]
        [Display(Name = "Url")]
        public string Url { get; set; } = string.Empty;
        [Display(Name = "Tipo de contenido")]
        public string? ContentType { get; set; } 
        [Display(Name ="Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name ="Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name ="Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name ="Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
