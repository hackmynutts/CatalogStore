namespace CatalogStore.UI.Models.ProductImage
{
    public class ProductImageViewModel
    {
        public int ProductImageID { get; set; }
        public int ProductID { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
