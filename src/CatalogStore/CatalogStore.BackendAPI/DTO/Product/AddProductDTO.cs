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
        [Display(Name = "Porcentaje de ganancia")]
        // Porcentaje que escribe el usuario (7 = 7 %); el servicio lo convierte al factor que guarda la entidad (1.07).
        // El tope de 900 % corresponde al CHECK de la base (factor máximo 10).
        [Range(0, 900, ErrorMessage = "El porcentaje de ganancia debe estar entre 0 y 900.")]
        public decimal ProfitPercentage { get; set; } = 7m;
        [Display(Name = "Precio unitario")]
        [Required(ErrorMessage = "Indique el precio unitario.")]
        public decimal? Price { get; set; }
        [Display(Name = "Precio con IVA")]
        public decimal? PriceCalcIVA { get; set; }
        [Display(Name = "Unidad de medida")]
        [Required(ErrorMessage = "El campo {0} es obligatorio.")]
        public Unit UnidadMedida { get; set; } = Unit.Unidad;
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
