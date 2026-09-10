using CatalogStore.BackendAPI.Models.Product;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Product
{
    public class ProductDTO
    {
        [Display(Name ="#")]
        public int ProductID { get; set; }
        [Display(Name ="Codigo")]
        public string? ProductCode { get; set; }
        [Display(Name ="Nombre")]
        public string ProductName { get; set; } = string.Empty;
        [Display(Name ="Descripción")]
        public string ProductDesc { get; set; } = string.Empty;
        [Display(Name ="Categoría")]
        public categoryType categoria { get; set; } = categoryType.General;
        [Display(Name ="Precio unitario")]
        public decimal? Price { get; set; }
        [Display(Name = "Unidad de medida")]
        public unit UnidadMedida { get; set; } = unit.Pieza;
        [Display(Name ="Estado")]
        public int StatusID { get; set; }
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
