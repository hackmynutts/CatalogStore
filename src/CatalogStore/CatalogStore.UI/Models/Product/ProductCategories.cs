namespace CatalogStore.UI.Models.Product
{
    // Categorías de producto: único lugar de la UI donde se escriben. Los números son los de categoryType del backend
    // (Models/Product/Product.cs) y deben coincidir siempre: la API los recibe y los devuelve como enteros.
    // Una categoría nueva se agrega aquí y en ese enum; los filtros, listados, formularios y la página de CABYS la toman sola.
    public static class ProductCategories
    {
        public static readonly (int Id, string Label)[] All =
        {
            (0, "Otros"),
            (1, "Puños"),
            (2, "Guantes"),
            (3, "Capas"),
            (4, "Plásticos de moto"),
            (5, "Caucho campana"),
            (6, "Jacket"),
            (7, "Filtros de aire"),
            (8, "Filtros de aceite"),
            (9, "Espejos"),
            (10, "Descansa pies"),
            (11, "Deslizador de cadena")
        };

        public static string Label(int id, string fallback = "—") =>
            All.Where(c => c.Id == id).Select(c => c.Label).FirstOrDefault() ?? fallback;
    }
}
