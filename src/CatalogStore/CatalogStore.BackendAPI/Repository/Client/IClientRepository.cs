using CatalogStore.BackendAPI.Models.Client;

namespace CatalogStore.BackendAPI.Repository.Client
{
    public interface IClientRepository
    {
        Task<List<Models.Client.Client>> GetAllClientsAsync();
        Task<List<Models.Client.Client>> GetActiveClientsAsync();
        Task<Models.Client.Client> GetClientAsync(int ID);
        Task<int>AddAsync(Models.Client.Client client);
        Task<bool>UpdateAsync(Models.Client.Client client);
        Task<bool>DeleteAsync(int ID);
    }
}
