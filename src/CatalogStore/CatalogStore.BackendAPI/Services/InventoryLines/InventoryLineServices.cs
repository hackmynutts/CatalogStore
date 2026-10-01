using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryLines;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryLine;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Inventory;
using CatalogStore.BackendAPI.Services.Product;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Services.InventoryLines
{
    public class InventoryLineServices : IInventoryLineServices
    {
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IInventoryServices _inventoryServices;
        private readonly IProductServices _productServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;
        public InventoryLineServices(IInventoryLineRepository inventoryLineRepository, IInventoryServices inventoryServices, IProductServices productServices, IEventlogServices eventlogServices, TimeProvider time)
        {
            _inventoryLineRepository = inventoryLineRepository;
            _inventoryServices = inventoryServices;
            _productServices = productServices;
            _eventlogServices = eventlogServices;
            _time = time;
        }
        public async Task<List<InventoryLineDTO>> GetByInventoryAsync(int inventoryID) => (await _inventoryLineRepository.GetByInventoryAsync(inventoryID)).Select(ToDto).ToList();

        public async Task<InventoryLineDTO?> GetByIdAsync(int inventoryLineID)
        {
            var line = await _inventoryLineRepository.GetByIdAsync(inventoryLineID);
            return line is null ? null : ToDto(line);
        }

        private static InventoryLineDTO ToDto(InventoryLine l) => new()
        {
            InventoryLineID = l.InventoryLineID,
            InventoryID = l.InventoryID,
            ProductID = l.ProductID,
            ProductCode = l.Product?.ProductCode ?? string.Empty,
            ProductName = l.Product?.ProductName ?? string.Empty,
            Quantity = l.Quantity,
            QuantityOnHold = l.QuantityOnHold,
            QuantityAvailable = l.QuantityAvailable,
            QuantityRestock = l.QuantityRestock,
            NeedsRestock = l.QuantityRestock > 0 && l.QuantityAvailable <= l.QuantityRestock,
            LastRestock = l.LastRestock,
            StatusID = l.StatusID,
            CreatedBy = l.CreatedBy,
            CreatedOn = l.CreatedOn,
            ModifiedBy = l.ModifiedBy,
            ModifiedOn = l.ModifiedOn
        };
        public async Task<OperationResult> AddAsync(AddInventoryLineDTO dto)
        {
            const string module = "Administracion/Inventario";
            try
            {
                var inventory = await _inventoryServices.GetInventoryAsync(dto.InventoryID);
                if (inventory is null || inventory.StatusID != 1)
                    return await FailAddAsync(dto, "La bodega no existe o está inactiva.");

                var product = await _productServices.GetProductAsync(dto.ProductID);
                if (product is null || product.StatusID != 1)
                    return await FailAddAsync(dto, "El producto no existe o está inactivo.");

                if (await _inventoryLineRepository.GetByInventoryAndProductAsync(dto.InventoryID, dto.ProductID) is not null)
                    return await FailAddAsync(dto, "Este producto ya tiene una línea en esta bodega.");

                var inventoryLine = new InventoryLine
                {
                    InventoryID = dto.InventoryID,
                    ProductID = dto.ProductID,
                    Quantity = 0,
                    QuantityOnHold = 0,
                    QuantityRestock = dto.QuantityRestock,
                    StatusID = 1,
                    CreatedBy = dto.CreatedBy,
                    CreatedOn = _time.GetUtcNow().UtcDateTime
                };

                int id = await _inventoryLineRepository.AddAsync(inventoryLine);
                await _eventlogServices.LogAsync(typeEvent.Add, module, "InventoryLine", id.ToString(),
                    "Línea de inventario creada.", postData: dto);
                return OperationResult.Ok(id);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Dos usuarios crearon la misma línea a la vez: el índice único rechazó la segunda.
                return await FailAddAsync(dto, "Este producto ya tiene una línea en esta bodega.");
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(typeEvent.AddFail, module, "InventoryLine", dto.InventoryID.ToString(),
                    "Excepción no controlada al crear una línea de inventario.", postData: dto, stackTrace: ex.ToString());
                throw;
            }
        }

        // Registra el rechazo de negocio en la bitácora y lo devuelve como resultado.
        private async Task<OperationResult> FailAddAsync(AddInventoryLineDTO dto, string error)
        {
            await _eventlogServices.LogAsync(typeEvent.AddFail, "Administracion/Inventario", "InventoryLine",
                dto.InventoryID.ToString(), error, postData: dto);
            return OperationResult.Fail(error);
        }
        public Task<OperationResult> UpdateAsync(UpdateInventoryLineDTO dto) =>
            ApplyChangesAsync(dto.InventoryLineID, dto.QuantityRestock, dto.StatusID, dto.ModifiedBy ?? "Sistema", typeEvent.Edit, typeEvent.EditFail);

        public Task<OperationResult> InactivateAsync(int inventoryLineID, string modifiedBy) =>
            ApplyChangesAsync(inventoryLineID, null, 2, modifiedBy, typeEvent.Inactivate, typeEvent.InactivateFail);

        private async Task<OperationResult> ApplyChangesAsync(int id, int? quantityRestock, int statusId, string modifiedBy, typeEvent ok, typeEvent fail)
        {
            const string module = "Administracion/Inventario";
            if (statusId is not (1 or 2))
                return OperationResult.Fail("Estado inválido.");

            try
            {
                // La misma entidad con seguimiento se valida y se guarda: RowVersion cubre todo el tramo.
                var line = await _inventoryLineRepository.GetForUpdateAsync(id);
                if (line is null)
                    return OperationResult.Fail("La línea de inventario no existe.");

                var before = new { line.QuantityRestock, line.StatusID, line.Quantity, line.QuantityOnHold };

                if (statusId == 2 && (line.Quantity > 0 || line.QuantityOnHold > 0))
                {
                    const string error = "No se puede inactivar una línea con existencias o reservas.";
                    await _eventlogServices.LogAsync(fail, module, "InventoryLine", id.ToString(), error, preData: before);
                    return OperationResult.Fail(error);
                }

                if (quantityRestock.HasValue) line.QuantityRestock = quantityRestock.Value;
                line.StatusID = statusId;
                line.ModifiedBy = modifiedBy;
                line.ModifiedOn = _time.GetUtcNow().UtcDateTime;

                await _inventoryLineRepository.SaveChangesAsync();
                await _eventlogServices.LogAsync(ok, module, "InventoryLine", id.ToString(), "Línea de inventario actualizada.",
                    preData: before, postData: new { line.QuantityRestock, line.StatusID });
                return OperationResult.Ok(id);
            }
            catch (DbUpdateConcurrencyException)
            {
                return OperationResult.Fail("La línea cambió mientras la editabas. Recargá e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(fail, module, "InventoryLine", id.ToString(),
                    "Excepción no controlada al actualizar una línea de inventario.", stackTrace: ex.ToString());
                throw;
            }
        }
    }
}
