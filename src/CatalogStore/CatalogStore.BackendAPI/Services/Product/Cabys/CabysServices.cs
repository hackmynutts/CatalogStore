using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogStore.BackendAPI.Services.Product.Cabys
{
    // Un código del catálogo oficial CABYS de Hacienda.
    public record CabysItem(string Code, string Description, decimal TaxRate, string Category);

    public interface ICabysServices
    {
        // Busca por texto en el catálogo oficial (máximo 100 resultados). Texto de menos de 3 letras: lista vacía.
        // Lanza HttpRequestException / TaskCanceledException si Hacienda no responde.
        Task<List<CabysItem>> SearchAsync(string text, CancellationToken ct = default);
        // El código exacto, o null si no existe en el catálogo. Mismos errores que SearchAsync si Hacienda no responde.
        Task<CabysItem?> GetAsync(string code, CancellationToken ct = default);
    }

    // API pública de Hacienda (cliente "HaciendaApi", sin autenticación). El catálogo es oficial y la factura exige un
    // código que exista, por eso se valida aquí antes de guardarlo en un producto.
    public class CabysServices : ICabysServices
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public CabysServices(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        public async Task<List<CabysItem>> SearchAsync(string text, CancellationToken ct = default)
        {
            text = text?.Trim() ?? string.Empty;
            if (text.Length < 3) return new List<CabysItem>();

            var client = _httpClientFactory.CreateClient("HaciendaApi");
            using var response = await client.GetAsync($"fe/cabys?q={Uri.EscapeDataString(text)}", ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct);
            return (result?.Cabys ?? new List<Entry>()).Select(ToItem).ToList();
        }

        public async Task<CabysItem?> GetAsync(string code, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient("HaciendaApi");
            using var response = await client.GetAsync($"fe/cabys?codigo={Uri.EscapeDataString(code)}", ct);
            response.EnsureSuccessStatusCode();
            // Con "codigo" responde una lista (vacía si no existe), no el objeto de la búsqueda por texto.
            var list = await response.Content.ReadFromJsonAsync<List<Entry>>(cancellationToken: ct);
            var found = list?.FirstOrDefault(e => e.Codigo == code);
            return found is null ? null : ToItem(found);
        }

        private static CabysItem ToItem(Entry e) =>
            new(e.Codigo, e.Descripcion, e.Impuesto, e.Categorias.LastOrDefault() ?? string.Empty);

        private class SearchResponse
        {
            [JsonPropertyName("cabys")] public List<Entry> Cabys { get; set; } = new();
        }

        private class Entry
        {
            [JsonPropertyName("codigo")] public string Codigo { get; set; } = string.Empty;
            [JsonPropertyName("descripcion")] public string Descripcion { get; set; } = string.Empty;
            [JsonPropertyName("categorias")] public List<string> Categorias { get; set; } = new();
            [JsonPropertyName("impuesto")] public decimal Impuesto { get; set; }
        }
    }
}
