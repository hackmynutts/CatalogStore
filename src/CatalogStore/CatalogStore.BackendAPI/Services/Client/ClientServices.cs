using CatalogStore.BackendAPI.DTO.Client;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Client;
using CatalogStore.BackendAPI.Services.Client.Hacienda;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Client
{
    public class ClientServices : IClientServices
    {
        private const string ModuleName = "Ventas/Clientes";
        private const string ClientsTable = "Client";

        private readonly IClientRepository _clientRepository;
        private readonly IHaciendaServices _haciendaServices;
        private readonly IEventlogServices _eventlogServices;

        public ClientServices(IClientRepository clientRepository, IHaciendaServices haciendaServices, IEventlogServices eventlogServices)
        {
            _clientRepository = clientRepository;
            _haciendaServices = haciendaServices;
            _eventlogServices = eventlogServices;
        }

        public async Task<List<Models.Client.Client>> GetAllClientsAsync() => await _clientRepository.GetAllClientsAsync();
        public async Task<List<Models.Client.Client>> GetActiveClientsAsync() => await _clientRepository.GetActiveClientsAsync();
        public async Task<Models.Client.Client> GetClientAsync(int ID) => await _clientRepository.GetClientAsync(ID);

        public async Task<HaciendaLookupResponseDTO> LookupAsync(string? identification)
        {
            if (string.IsNullOrWhiteSpace(identification))
                return new HaciendaLookupResponseDTO { Nombre = "No encontrado.", Actividades = null };

            var result = await _haciendaServices.LookupAsync(identification);
            return result ?? new HaciendaLookupResponseDTO { Nombre = "No encontrado.", Actividades = null };
        }

        public async Task<int> AddAsync(AddClientDTO dto, bool isAdmin)
        {
            Models.Client.Client client = new Models.Client.Client
            {
                Identification = dto.Identification,
                ClientName = dto.ClientName,
                ClientPhone = dto.ClientPhone,
                ClientEmail = dto.ClientEmail,
                ClientAddress = dto.ClientAddress,
                Credito = dto.Credito,
                Cabys = dto.cabys,
                DeliveryPartner = dto.DeliveryPartner,
                StatusID = 1,
                CreatedBy = dto.CreatedBy,
                CreatedOn = DateTime.UtcNow
            };
            try
            {
                //validacion de duplicidad de cliente por identificacion
                Models.Client.Client clientExist = await _clientRepository.GetClientByIdentificationAsync(dto.Identification);
                if (clientExist != null && !isAdmin)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.AddFail,
                        ModuleName,
                        ClientsTable,
                        dto.Identification,
                        "El cliente con la identificación proporcionada ya existe.",
                        postData: client);
                    return 0;
                }

                var id = await _clientRepository.AddAsync(client);

                await _eventlogServices.LogAsync(
                    typeEvent.Add,
                    ModuleName,
                    ClientsTable,
                    client.Id.ToString(),
                    "Cliente creado.",
                    postData: client);

                return id;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.AddFail,
                    ModuleName,
                    ClientsTable,
                    client.Identification,
                    "Excepción no controlada al crear un cliente.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> UpdateAsync(int id, UpdateClientDTO dto)
        {
            try
            {
                var existing = await _clientRepository.GetClientAsync(id);
                if (existing == null) return false;

                var before = new
                {
                    existing.Id,
                    existing.Identification,
                    existing.ClientName,
                    existing.ClientPhone,
                    existing.ClientEmail,
                    existing.ClientAddress,
                    existing.Credito,
                    existing.Cabys,
                    existing.DeliveryPartner,
                    existing.StatusID,
                    existing.CreatedBy,
                    existing.CreatedOn,
                    existing.ModifiedBy,
                    existing.ModifiedOn
                };

                existing.ClientName = dto.ClientName;
                existing.ClientPhone = dto.ClientPhone;
                existing.ClientEmail = dto.ClientEmail;
                existing.ClientAddress = dto.ClientAddress;
                existing.Credito = dto.Credito;
                existing.Cabys = dto.cabys;
                existing.DeliveryPartner = dto.DeliveryPartner;
                existing.StatusID = dto.StatusID;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.Now;

                var updated = await _clientRepository.UpdateAsync(existing);
                if (!updated)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        ModuleName,
                        ClientsTable,
                        id.ToString(),
                        "No se pudo actualizar el cliente (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Edit,
                    ModuleName,
                    ClientsTable,
                    id.ToString(),
                    "Cliente actualizado.",
                    preData: before,
                    postData: existing);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    ModuleName,
                    ClientsTable,
                    id.ToString(),
                    "Excepción no controlada al editar un cliente.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> InactivateAsync(int id, UpdateClientDTO dto)
        {
            try
            {
                var existing = await _clientRepository.GetClientAsync(id);
                if (existing == null) return false;

                var before = new
                {
                    existing.Id,
                    existing.Identification,
                    existing.ClientName,
                    existing.ClientPhone,
                    existing.ClientEmail,
                    existing.ClientAddress,
                    existing.Credito,
                    existing.Cabys,
                    existing.DeliveryPartner,
                    existing.StatusID,
                    existing.CreatedBy,
                    existing.CreatedOn,
                    existing.ModifiedBy,
                    existing.ModifiedOn
                };

                existing.StatusID = 2;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.UtcNow;

                var updated = await _clientRepository.UpdateAsync(existing);
                if (!updated)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.InactivateFail,
                        ModuleName,
                        ClientsTable,
                        id.ToString(),
                        "No se pudo inactivar el cliente (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Inactivate,
                    ModuleName,
                    ClientsTable,
                    id.ToString(),
                    "Cliente inactivado.",
                    preData: before,
                    postData: existing);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.InactivateFail,
                    ModuleName,
                    ClientsTable,
                    id.ToString(),
                    "Excepción no controlada al inactivar un cliente.",
                    stackTrace: ex.ToString());
                throw;
            }
        }

        public async Task<bool> DeleteAsync(int ID)
        {
            try
            {
                var before = await _clientRepository.GetClientAsync(ID);
                if (before == null) return false;

                var deleted = await _clientRepository.DeleteAsync(ID);
                if (!deleted)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.DeleteFail,
                        ModuleName,
                        ClientsTable,
                        ID.ToString(),
                        "No se pudo eliminar el cliente (posible conflicto de concurrencia o registro eliminado).",
                        preData: before);
                    return false;
                }

                await _eventlogServices.LogAsync(
                    typeEvent.Delete,
                    ModuleName,
                    ClientsTable,
                    ID.ToString(),
                    "Cliente eliminado.",
                    preData: before);

                return true;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    ModuleName,
                    ClientsTable,
                    ID.ToString(),
                    "Excepción no controlada al eliminar un cliente.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
    }
}
