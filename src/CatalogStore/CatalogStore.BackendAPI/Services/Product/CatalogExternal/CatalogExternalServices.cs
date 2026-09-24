using CatalogStore.BackendAPI.DTO.CatalogExternal;

namespace CatalogStore.BackendAPI.Services.Product.CatalogExternal
{
    public class CatalogExternalServices : ICatalogExternalServices
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        public CatalogExternalServices(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }
        public async Task<ExternalCatalogPageDTO> GetProductsPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("ExternalCatalog");
            var token = _configuration["ExternalCatalog:Token"];

            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("El token de catálogo externo no está configurado.");

            var response = await client.GetAsync($"ApiJsonInventariosItems.php?Token={Uri.EscapeDataString(token)}&accion=productos&page={page}&pagesize={pageSize}&include_total=1", cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Error al obtener la página de productos: {response.ReasonPhrase}", null, response.StatusCode);

            var productsPage = await response.Content.ReadFromJsonAsync<ExternalCatalogPageDTO>(cancellationToken: cancellationToken)
                                                        ?? throw new InvalidOperationException("Fallo al deserializar la página de productos.");

            if (!productsPage.Ok)
                throw new InvalidOperationException(
                    $"El catálogo externo respondió ok=false: {productsPage.Error ?? "sin detalle"}");

            return productsPage;
        }
    }
}
