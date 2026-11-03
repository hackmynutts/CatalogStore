namespace CatalogStore.UI.Models.Product
{
    // Categorías de producto: único lugar de la UI donde se escriben. Los números son los de categoryType del backend
    // (Models/Product/Product.cs) y deben coincidir siempre: la API los recibe y los devuelve como enteros.
    // Una categoría nueva se agrega aquí y en ese enum; los filtros, listados, formularios y la página de CABYS la toman sola.
    public static class ProductCategories
    {
        public static readonly (int Id, string Label)[] All =
        {
            (0, "Genérica"),
            (1, "Puños para moto"),
            (2, "Guantes de moto"),
            (3, "Capas de moto"),
            (4, "Plásticos de moto"),
            (5, "Caucho campana"),
            (6, "Jacket")
        };

        public static string Label(int id, string fallback = "—") =>
            All.Where(c => c.Id == id).Select(c => c.Label).FirstOrDefault() ?? fallback;
    }
}
