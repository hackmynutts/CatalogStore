using CatalogStore.BackendAPI.DTO.Client;

namespace CatalogStore.BackendAPI.Services.Client.Hacienda
{
    public interface IHaciendaServices
    {
        Task<HaciendaLookupResponseDTO?> LookupAsync(string identification);
    }
}
