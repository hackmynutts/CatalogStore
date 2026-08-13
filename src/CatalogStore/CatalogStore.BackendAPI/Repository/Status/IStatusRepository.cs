namespace CatalogStore.BackendAPI.Repository.Status
{
    public interface IStatusRepository
    {
        Task<List<Models.Status.Status>> GetAllStatusesAsync();
        Task<Models.Status.Status> GetStatusAsync(int id);
        Task<int> AddAsync(Models.Status.Status status);
        Task<bool> UpdateAsync(Models.Status.Status status);
        Task<bool> DeleteAsync(Models.Status.Status status);
    }
}
