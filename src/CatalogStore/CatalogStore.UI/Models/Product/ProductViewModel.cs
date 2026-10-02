using CatalogStore.UI.Models.ProductImage;

namespace CatalogStore.UI.Models.Product
{
    public class ProductViewModel
    {
        public int ProductID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductDesc { get; set; } = string.Empty;
        public int categoria { get; set; }
        public decimal? Price { get; set; }
        // Mismo nombre que en el backend para que el JSON ("priceCalcIVA") se mapee solo.
        // Nunca es null: los productos sin precio tienen Price = null y PriceCalcIVA = 0.
        public decimal PriceCalcIVA { get; set; }
        public int UnidadMedida { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public List<ProductImageViewModel> Images { get; set; } = new List<ProductImageViewModel>();
    }
}
