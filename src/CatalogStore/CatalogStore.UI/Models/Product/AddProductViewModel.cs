namespace CatalogStore.UI.Models.Product
{
    public class AddProductViewModel
    {
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductDesc { get; set; } = string.Empty;
        public int categoria { get; set; }
        // Porcentaje que escribe el usuario (7 = 7 %). El backend lo convierte al factor 1.07.
        public decimal ProfitPercentage { get; set; } = 7m;
        public decimal? Price { get; set; }
        public int UnidadMedida { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
