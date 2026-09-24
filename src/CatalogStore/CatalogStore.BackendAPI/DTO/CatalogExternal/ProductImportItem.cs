using CatalogStore.BackendAPI.Models.Product;

namespace CatalogStore.BackendAPI.DTO.CatalogExternal
{
    public class ProductImportItem
    {
        public int ExternalProductID { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductDesc { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public decimal PriceCalcIVA { get; set; }
        public Unit UnidadMedida { get; set; } = Unit.Unidad;
        public int StatusID { get; set; } = 1;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }
}
