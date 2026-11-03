namespace CatalogStore.UI.Models.Home
{
    // Producto tal como lo ve el público en la landing: sin precio ni existencias.
    public class PublicCatalogItem
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Category { get; set; }
        // Ya con la dirección pública del backend; null si el producto no tiene foto.
        public string? ImageUrl { get; set; }

        public static string CategoryLabel(int category) => Product.ProductCategories.Label(category, "Otros");
    }
}
