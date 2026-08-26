using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.Status;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Status
{
    public class StatusServices : IStatusServices
    {
        private readonly Repository.Status.IStatusRepository _statusRepository;
        private readonly IEventlogServices _eventlogServices;
        public StatusServices(Repository.Status.IStatusRepository statusRepository, IEventlogServices services)
        {
            _statusRepository = statusRepository;
            _eventlogServices = services;
        }

        public async Task<List<Models.Status.Status>> GetAllStatusesAsync() => await _statusRepository.GetAllStatusesAsync();
        public async Task<Models.Status.Status> GetStatusAsync(int id) => await _statusRepository.GetStatusAsync(id);
        public async Task<int> AddAsync(DTO.Status.AddStatusDTO status)
        {
            try
            {
                Models.Status.Status estado = new Models.Status.Status
                {
                    name = status.name,
                    CreatedBy = status.CreatedBy,
                    CreatedOn = DateTime.UtcNow
                };
                await _statusRepository.AddAsync(estado);
                await _eventlogServices.LogAsync(
                                    typeEvent.Add,
                                    "Administration/Status", 
                                    "Status", 
                                    estado.StatusID.ToString(),
                                    "Estado creado.", 
                                    postData: estado);
                return estado.StatusID;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.AddFail,
                    "Administration/Status",
                    "Status",
                    status.name,
                    "Excepción no controlada al crear un estado.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> UpdateAsync(DTO.Status.UpdateStatusDTO status)
        {
            Models.Status.Status existingStatus = await _statusRepository.GetStatusAsync(status.StatusID);
            if (existingStatus == null) return false;
            try
            {
                var before = new
                {
                    existingStatus.StatusID,
                    existingStatus.name,
                    existingStatus.CreatedBy,
                    existingStatus.CreatedOn,
                    existingStatus.UpdatedBy,
                    existingStatus.UpdatedOn
                };

                existingStatus.name = status.name;
                existingStatus.UpdatedBy = status.UpdatedBy;
                existingStatus.UpdatedOn = DateTime.UtcNow;

                if (await _statusRepository.UpdateAsync(existingStatus))
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Edit,
                        "Administration/Status",
                        "Status",
                        status.StatusID.ToString(),
                        "Estado actualizado.",
                        preData: before,
                        postData: existingStatus);
                    return true;
                }

                await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        "Administration/Status",
                        "Status",
                        status.StatusID.ToString(),
                        "No se pudo actualizar el estado (posible conflicto de concurrencia o registro eliminado).");
                return false;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    "Administration/Status",
                    "Status",
                    status.StatusID.ToString(),
                    "Excepción no controlada al editar un estado.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            Models.Status.Status status = await _statusRepository.GetStatusAsync(id);
            if (status == null) return false;
            try
            {
                var before = new
                {
                    status.StatusID,
                    status.name,
                    status.CreatedBy,
                    status.CreatedOn,
                    status.UpdatedBy,
                    status.UpdatedOn
                };
                if (await _statusRepository.DeleteAsync(status))
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Delete,
                        "Administration/Status",
                        "Status",
                        status.StatusID.ToString(),
                        "Estado eliminado correctamente.",
                        preData: before,
                        postData: null);
                    return true;
                }

                await _eventlogServices.LogAsync(
                        typeEvent.DeleteFail,
                        "Administration/Status",
                        "Status",
                        status.StatusID.ToString(),
                        "No se pudo eliminar el estado (posible conflicto de concurrencia o registro eliminado).");
                return false;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    "Administration/Status",
                    "Status",
                    status.StatusID.ToString(),
                    "Excepción no controlada al eliminar un estado.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

    }
}
