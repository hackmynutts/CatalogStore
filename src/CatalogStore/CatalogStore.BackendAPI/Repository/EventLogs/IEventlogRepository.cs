using CatalogStore.BackendAPI.Models.EventLogs;

namespace CatalogStore.BackendAPI.Repository.EventLogs
{
    public interface IEventlogRepository
    {
        Task<List<Eventlog>> GetAllEventsAsync();
        Task AddAsync(Eventlog eventlog);
    }
}
