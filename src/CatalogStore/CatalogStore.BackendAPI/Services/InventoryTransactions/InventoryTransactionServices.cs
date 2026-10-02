using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryTransactions;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Repository.InventoryTransactions;
using CatalogStore.BackendAPI.Services.EventLogs;
using Humanizer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    // Único lugar del sistema que mueve stock. Cada movimiento actualiza la línea e inserta su transacción
    // en un solo SaveChanges: o se guardan las dos cosas o ninguna.
    public class InventoryTransactionServices : IInventoryTransactionServices
    {
        private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;
        private const string module = "Administracion/Inventario/Transacciones";
        private const string table = "InventoryTransaction";

        public InventoryTransactionServices(IInventoryTransactionRepository inventoryTransactionRepository, IInventoryLineRepository inventoryLineRepository, IEventlogServices eventlogServices, TimeProvider time)
        {
            _inventoryTransactionRepository = inventoryTransactionRepository;
            _inventoryLineRepository = inventoryLineRepository;
            _eventlogServices = eventlogServices;
            _time = time;
        }

        public async Task<List<InventoryTransactionDTO>> GetByLineAsync(int inventoryLineId) =>
            (await _inventoryTransactionRepository.GetByLineAsync(inventoryLineId)).Select(ToDto).ToList();

        public async Task<OperationResult> RegisterAsync(RegisterInventoryTransactionDTO dto, string createdBy)
        {
            var error = ValidateRequest(dto);
            if (error is not null)
                return OperationResult.Fail(error);

            try
            {
                // Entidad con seguimiento: si otro usuario la mueve antes del SaveChanges, RowVersion lo detecta.
                var line = await _inventoryLineRepository.GetForUpdateAsync(dto.InventoryLineID);
                if (line is null)
                    return OperationResult.Fail("La línea de inventario no existe.");
                if (line.StatusID != 1)
                    return OperationResult.Fail("La línea de inventario no está activa.");

                // && corta antes de consultar la base si el movimiento no es una carga inicial.
                if (dto.Type == TransactionType.CargaInicial && await _inventoryTransactionRepository.HasTransactionsAsync(line.InventoryLineID))
                    return OperationResult.Fail("La línea ya tiene movimientos; usá un incremento de stock o un ajuste.");

                var effect = GetEffect(dto.Type, dto.TransactionQuantity);
                int qtyBefore = line.Quantity;
                int onHoldBefore = line.QuantityOnHold;
                int qtyAfter = qtyBefore + effect.Stock;
                int onHoldAfter = onHoldBefore + effect.OnHold;

                // Estas dos reglas cubren toda la tabla de efectos (son los mismos CHECK de la tabla):
                // liberar o vender más de lo reservado, y reservar o ajustar por encima del disponible.
                if (onHoldAfter < 0)
                    return OperationResult.Fail($"No hay suficiente reservado. Solo hay {onHoldBefore} unidades reservadas.");
                if (onHoldAfter > qtyAfter)
                    return OperationResult.Fail($"No hay suficiente disponible. Solo hay {line.QuantityAvailable} unidades disponibles.");

                var now = _time.GetUtcNow().UtcDateTime;

                line.Quantity = qtyAfter;
                line.QuantityOnHold = onHoldAfter;
                line.ModifiedBy = createdBy;
                line.ModifiedOn = now;
                if (dto.Type is TransactionType.CargaInicial or TransactionType.IncrementoStock)
                    line.LastRestock = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

                var transaction = new InventoryTransaction
                {
                    InventoryLineID = line.InventoryLineID,
                    Type = dto.Type,
                    TransactionQuantity = dto.TransactionQuantity,
                    QuantityBefore = qtyBefore,
                    QuantityAfter = qtyAfter,
                    OnHoldBefore = onHoldBefore,
                    OnHoldAfter = onHoldAfter,
                    ReferenceReason = dto.ReferenceReason,
                    ReferenceID = CleanText(dto.ReferenceID),
                    Reason = CleanText(dto.Reason),
                    CreatedBy = createdBy,
                    CreatedOn = now
                };

                // Add no guarda; el SaveChanges del repositorio de líneas guarda la línea y la transacción
                // juntas porque los dos repositorios comparten el mismo DbContext del request.
                _inventoryTransactionRepository.Add(transaction);
                await _inventoryLineRepository.SaveChangesAsync();

                return OperationResult.Ok(transaction.InventoryTransactionID);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Esperado: otro movimiento cambió la línea en el medio. EF revierte la línea y la transacción.
                return OperationResult.Fail("El stock de esta línea cambió mientras se procesaba. Intentá de nuevo.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
            {
                // No debería pasar: las validaciones de arriba replican los CHECK. Si pasa, es un bug.
                const string message = "El movimiento dejaría el inventario en un estado inválido.";
                await _eventlogServices.LogAsync(typeEvent.AddFail, module, table, dto.InventoryLineID.ToString(),
                    message, postData: dto, stackTrace: ex.ToString());
                return OperationResult.Fail(message);
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(typeEvent.AddFail, module, table, dto.InventoryLineID.ToString(),
                    "Excepción no controlada al registrar un movimiento de inventario.", postData: dto, stackTrace: ex.ToString());
                throw;
            }
        }

        // Validaciones que no necesitan la base de datos. Se repiten aunque el DTO tenga atributos,
        // porque otros servicios (órdenes, importación) llaman aquí sin pasar por el model binding.
        private static string? ValidateRequest(RegisterInventoryTransactionDTO dto)
        {
            if (dto.InventoryLineID <= 0)
                return "La línea de inventario no es válida.";
            if (dto.Type == TransactionType.None || !Enum.IsDefined(dto.Type))
                return "El tipo de movimiento no es válido.";
            if (dto.TransactionQuantity <= 0)
                return "La cantidad debe ser mayor a cero.";
            if (dto.Type is TransactionType.AjustePositivo or TransactionType.AjusteNegativo && string.IsNullOrWhiteSpace(dto.Reason))
                return "Los ajustes requieren una razón.";
            if (dto.Type is TransactionType.ProductoReservado or TransactionType.ProductoLiberado or TransactionType.Venta && string.IsNullOrWhiteSpace(dto.ReferenceID))
                return "Los movimientos de órdenes requieren el número de orden.";
            return null;
        }

        // Tabla de efectos: cuánto cambia el stock y cuánto el reservado según el tipo.
        private static (int Stock, int OnHold) GetEffect(TransactionType type, int q) => type switch
        {
            TransactionType.CargaInicial or TransactionType.IncrementoStock or TransactionType.AjustePositivo => (q, 0),
            TransactionType.ProductoReservado => (0, q),
            TransactionType.ProductoLiberado => (0, -q),
            TransactionType.Venta => (-q, -q),
            TransactionType.AjusteNegativo => (-q, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private static string? CleanText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static InventoryTransactionDTO ToDto(InventoryTransaction t) => new()
        {
            InventoryTransactionID = t.InventoryTransactionID,
            InventoryLineID = t.InventoryLineID,
            Type = t.Type,
            TypeName = t.Type.Humanize(),
            TransactionQuantity = t.TransactionQuantity,
            QuantityBefore = t.QuantityBefore,
            QuantityAfter = t.QuantityAfter,
            OnHoldBefore = t.OnHoldBefore,
            OnHoldAfter = t.OnHoldAfter,
            StockChange = t.QuantityAfter - t.QuantityBefore,
            OnHoldChange = t.OnHoldAfter - t.OnHoldBefore,
            ReferenceReason = t.ReferenceReason,
            ReferenceReasonName = t.ReferenceReason.Humanize(),
            ReferenceID = t.ReferenceID,
            Reason = t.Reason,
            CreatedBy = t.CreatedBy,
            CreatedOn = t.CreatedOn
        };
    }
}
