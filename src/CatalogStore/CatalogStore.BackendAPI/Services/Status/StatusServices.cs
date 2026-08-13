using CatalogStore.BackendAPI.Models.Status;

namespace CatalogStore.BackendAPI.Services.Status
{
    public class StatusServices : IStatusServices
    {
        private readonly Repository.Status.IStatusRepository _statusRepository;
        public StatusServices(Repository.Status.IStatusRepository statusRepository)
        {
            _statusRepository = statusRepository;
        }

        public async Task<List<Models.Status.Status>> GetAllStatusesAsync() => await _statusRepository.GetAllStatusesAsync();
        public async Task<Models.Status.Status> GetStatusAsync(int id) => await _statusRepository.GetStatusAsync(id);
        public async Task<int> AddAsync(DTO.Status.AddStatusDTO status)
        {
            Models.Status.Status estado = new Models.Status.Status
            {
                name = status.name,
                CreatedBy = status.CreatedBy,
                CreatedOn = DateTime.UtcNow
            };
            await _statusRepository.AddAsync(estado);
            return estado.StatusID;
        }
        public async Task<bool> UpdateAsync(DTO.Status.UpdateStatusDTO status)
        {
            Models.Status.Status existingStatus = await _statusRepository.GetStatusAsync(status.StatusID);
            if (existingStatus == null) return false;

            existingStatus.name = status.name;
            existingStatus.UpdatedBy = status.UpdatedBy;
            existingStatus.UpdatedOn = DateTime.UtcNow;
            return await _statusRepository.UpdateAsync(existingStatus);
        }
        public async Task<bool> DeleteAsync(int id)
        {
            Models.Status.Status status = await _statusRepository.GetStatusAsync(id);
            if (status == null) return false;
            return await _statusRepository.DeleteAsync(status);
        }

    }
}
