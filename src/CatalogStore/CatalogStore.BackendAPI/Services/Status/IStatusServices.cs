namespace CatalogStore.BackendAPI.Services.Status
{
    public interface IStatusServices
    {
        Task<List<Models.Status.Status>> GetAllStatusesAsync();
        Task<Models.Status.Status> GetStatusAsync(int id);
        Task<int> AddAsync(DTO.Status.AddStatusDTO status);
        Task<bool> UpdateAsync(DTO.Status.UpdateStatusDTO status);
        Task<bool> DeleteAsync(int id);
    }
}
