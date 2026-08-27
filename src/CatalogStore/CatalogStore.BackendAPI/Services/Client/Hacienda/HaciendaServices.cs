using CatalogStore.BackendAPI.DTO.Client;

namespace CatalogStore.BackendAPI.Services.Client.Hacienda
{
    public class HaciendaServices : IHaciendaServices
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public HaciendaServices(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<HaciendaLookupResponseDTO?> LookupAsync(string identification)
        {
            var client = _httpClientFactory.CreateClient("HaciendaApi");
            var response = await client.GetAsync($"fe/ae?identificacion={identification}");

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<HaciendaLookupResponseDTO>();
        }
    }
}
