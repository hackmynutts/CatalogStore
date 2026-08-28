namespace CatalogStore.UI.Models.Product
{
    public class UpdateProductViewModel
    {
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductDesc { get; set; } = string.Empty;
        public int categoria { get; set; }
        public decimal? Price { get; set; }
        public int StatusID { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
