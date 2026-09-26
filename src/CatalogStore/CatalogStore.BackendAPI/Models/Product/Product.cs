using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Product
{
    public enum categoryType
    {
        [Display(Name ="Generica")]
        General = 0,
        [Display(Name ="Repuestos de moto")]
        Repuestos = 1,
        [Display(Name = "Accesorios de moto")]
        Accesorios = 2,
        [Display(Name = "Capas de moto")]
        Capas = 3,
        [Display(Name = "Plasticos de moto")]
        Tapas = 4
    }
    public enum Unit
    {
        [Display(Name = "Pieza")]
        Pieza = 0,
        [Display(Name = "Litro")]
        Litro = 1,
        [Display(Name = "Caja")]
        Caja = 2,
        [Display(Name = "Paquete")]
        Paquete = 3,
        [Display(Name = "Unidad")]
        Unidad = 4,
        [Display(Name = "Desconocido")]
        Unknown = 5
    }
    [Table("Products_TB")]
    public class Product
    {
        public int ProductID{ get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductDesc{ get; set; } = string.Empty;
        public categoryType categoria { get; set; } = categoryType.General;
        public decimal? Price { get; set; }
        public decimal PriceCalcIVA { get; set; }
        public Unit UnidadMedida { get; set; } = Unit.Pieza;
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public int? ExternalProductID { get; set; }
        public Models.Status.Status Status { get; set; } = null!;
        public ICollection<BackendAPI.Models.ProductImage.ProductImage> Images { get; set; } = new List<BackendAPI.Models.ProductImage.ProductImage>();
    }
}
