using CatalogStore.BackendAPI.DTO.Inventory;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Inventory;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Inventory
{
    public class InventoryServices : IInventoryServices
    {
        private const string ModuleName = "Administracion/Inventario";
        private readonly IInventoryRepository _repository;
        private readonly IEventlogServices _eventlogServices;
        public InventoryServices(IInventoryRepository repository, IEventlogServices eventlogServices)
        {
            _repository = repository;
            _eventlogServices = eventlogServices;
        }
        public async Task<List<Models.Inventory.Inventory>> GetAllInventoriesAsync() => await _repository.GetAllInventoriesAsync();
        public async Task<List<Models.Inventory.Inventory>> GetActiveInventoriesAsync() => await _repository.GetActiveInventoriesAsync();
        public async Task<Models.Inventory.Inventory> GetInventoryAsync(int ID) => await _repository.GetInventoryAsync(ID);
        public async Task<int> AddAsync(AddInventoryDTO inventory)
        {
            Models.Inventory.Inventory newInventory = new Models.Inventory.Inventory
            {
                Name = inventory.Name,
                Descripcion = inventory.Descripcion,
                StatusID = 1,
                CreatedBy = inventory.CreatedBy,
                CreatedOn = DateTime.Now
            };
            try
            {
                int id = await _repository.AddAsync(newInventory);
                await _eventlogServices.LogAsync(
                    typeEvent.Add,
                    ModuleName,
                    "Inventory",
                    id.ToString(),
                    "Inventario creado.",
                    postData: inventory);
                return id;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.AddFail,
                    ModuleName,
                    "Inventory",
                    inventory.Name,
                    "Excepción no controlada al crear un inventario.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> UpdateAsync(UpdateInventoryDTO dto)
        {
            try
            {
                var existing = await _repository.GetInventoryAsync(dto.InventoryID);
                if (existing == null)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.EditFail,
                        ModuleName,
                        "Inventory",
                        dto.InventoryID.ToString(),
                        "Inventario no encontrado para actualizar.",
                        postData: dto);
                    return false;
                }

                var before = new
                {
                    InventoryID = existing.InventoryID,
                    Name = existing.Name,
                    Descripcion = existing.Descripcion,
                    StatusID = existing.StatusID,
                    CreatedBy = existing.CreatedBy,
                    CreatedOn = existing.CreatedOn,
                    ModifiedBy = existing.ModifiedBy,
                    ModifiedOn = existing.ModifiedOn
                };

                existing.Name = dto.Name;
                existing.Descripcion = dto.Descripcion;
                existing.StatusID = dto.StatusID;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.Now;

                bool result = await _repository.UpdateAsync(existing);
                if (result)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Edit,
                        ModuleName,
                        "Inventory",
                        dto.InventoryID.ToString(),
                        "Inventario actualizado.",
                        preData: before,
                        postData: dto);
                }
                return result;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.EditFail,
                    ModuleName,
                    "Inventory",
                    dto.InventoryID.ToString(),
                    "Excepción no controlada al actualizar un inventario.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> InactivateAsync(UpdateInventoryDTO dto)
        {
            try
            {
                var existing = await _repository.GetInventoryAsync(dto.InventoryID);
                if (existing == null)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.InactivateFail,
                        ModuleName,
                        "Inventory",
                        dto.InventoryID.ToString(),
                        "Inventario no encontrado para actualizar.",
                        postData: dto);
                    return false;
                }

                var before = new
                {
                    InventoryID = existing.InventoryID,
                    Name = existing.Name,
                    Descripcion = existing.Descripcion,
                    StatusID = existing.StatusID,
                    CreatedBy = existing.CreatedBy,
                    CreatedOn = existing.CreatedOn,
                    ModifiedBy = existing.ModifiedBy,
                    ModifiedOn = existing.ModifiedOn
                };

                existing.StatusID = 2;
                existing.ModifiedBy = dto.ModifiedBy;
                existing.ModifiedOn = DateTime.Now;

                bool result = await _repository.UpdateAsync(existing);
                if (result)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Edit,
                        ModuleName,
                        "Inventory",
                        dto.InventoryID.ToString(),
                        "Inventario inactivado.",
                        preData: before,
                        postData: dto);
                }
                return result;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.InactivateFail,
                    ModuleName,
                    "Inventory",
                    dto.InventoryID.ToString(),
                    "Excepción no controlada al inactivar un inventario.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var existing = await _repository.GetInventoryAsync(id);
                if (existing == null)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.DeleteFail,
                        ModuleName,
                        "Inventory",
                        id.ToString(),
                        "Inventario no encontrado para eliminar.");
                    return false;
                }
                bool result = await _repository.DeleteAsync(id);
                if (result)
                {
                    await _eventlogServices.LogAsync(
                        typeEvent.Delete,
                        ModuleName,
                        "Inventory",
                        id.ToString(),
                        "Inventario eliminado.");
                }
                return result;
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(
                    typeEvent.DeleteFail,
                    ModuleName,
                    "Inventory",
                    id.ToString(),
                    "Excepción no controlada al eliminar un inventario.",
                    stackTrace: ex.ToString());
                throw;
            }
        }
    }
}
