using CatalogStore.BackendAPI.Models.Product;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Product
{
    public class UpdateProductDTO
    {
        [Display(Name = "Codigo")]
        public string? ProductCode { get; set; }
        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ProductName { get; set; } = string.Empty;
        [Display(Name = "Descripcion")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ProductDesc { get; set; } = string.Empty;
        [Display(Name = "Categoria")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public categoryType categoria { get; set; } = categoryType.General;
        [Display(Name = "Precio Unitario")]
        [Required(ErrorMessage = "Indique el precio unitario.")]
        public decimal? Price { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
