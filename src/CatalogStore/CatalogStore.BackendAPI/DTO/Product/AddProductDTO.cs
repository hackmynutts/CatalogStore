using CatalogStore.BackendAPI.Models.Product;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Product
{
    public class AddProductDTO
    {
        [Display(Name = "Codigo")]
        public string? ProductCode { get; set; }
        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ProductName { get; set; } = string.Empty;
        [Display(Name = "Descripción")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public string ProductDesc { get; set; } = string.Empty;
        [Display(Name = "Categoría")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public categoryType categoria { get; set; } = categoryType.General;
        [Display(Name = "Precio unitario")]
        [Required(ErrorMessage = "Indique el precio unitario.")]
        public decimal? Price { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
