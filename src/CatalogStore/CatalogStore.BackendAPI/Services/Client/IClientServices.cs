using CatalogStore.BackendAPI.DTO.Client;

namespace CatalogStore.BackendAPI.Services.Client
{
    public interface IClientServices
    {
        Task<List<Models.Client.Client>> GetAllClientsAsync();
        Task<List<Models.Client.Client>> GetActiveClientsAsync();
        Task<Models.Client.Client> GetClientAsync(int ID);
        Task<HaciendaLookupResponseDTO> LookupAsync(string? identification);
        Task<int> AddAsync(AddClientDTO dto);
        Task<bool> UpdateAsync(int id, UpdateClientDTO dto);
        Task<bool> InactivateAsync(int id, UpdateClientDTO dto);
        Task<bool> DeleteAsync(int ID);
    }
}
