using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryTransactions;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryLine;
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

        // Movimiento suelto: carga la línea, lo aplica y guarda. Siempre guarda, sea cual sea el tipo.
        public async Task<OperationResult> RegisterAsync(RegisterInventoryTransactionDTO dto, string createdBy)
        {
            if (dto.InventoryLineID <= 0)
                return OperationResult.Fail("La línea de inventario no es válida.");
            var error = ValidateMovement(dto.Type, dto.TransactionQuantity, dto.ReferenceID, dto.Reason);
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

                var now = _time.GetUtcNow().UtcDateTime;
                var (applyError, transaction) = ApplyCore(line, dto.Type, dto.TransactionQuantity,
                    dto.ReferenceReason, dto.ReferenceID, dto.Reason, createdBy, now);
                if (applyError is not null)
                    return OperationResult.Fail(applyError);

                // ApplyCore no guarda; el SaveChanges del repositorio de líneas guarda la línea y la transacción
                // juntas porque los dos repositorios comparten el mismo DbContext del request.
                await _inventoryLineRepository.SaveChangesAsync();

                return OperationResult.Ok(transaction!.InventoryTransactionID);
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

        // Para procesos que mueven varias líneas a la vez (órdenes): valida y aplica el movimiento sobre una línea
        // YA CARGADA con seguimiento, pero NO guarda. Quien llama guarda una sola vez al final, junto con su propia
        // entidad, así que se guarda todo o nada.
        // Si un movimiento falla después de haber aplicado otros, quien llama debe descartar los cambios pendientes
        // (ChangeTracker.Clear) ANTES de loguear o responder: el log usa el mismo DbContext y guardaría lo pendiente.
        public OperationResult ApplyMovement(InventoryLine line, TransactionType type, int quantity,
            ReferenceType referenceReason, string? referenceId, string? reason, string user, DateTime now)
        {
            var error = ValidateMovement(type, quantity, referenceId, reason);
            if (error is not null)
                return OperationResult.Fail(error);
            // La regla "solo en una línea sin movimientos" necesita una consulta a la base; eso lo hace RegisterAsync.
            if (type == TransactionType.CargaInicial)
                return OperationResult.Fail("La carga inicial solo se registra con RegisterAsync.");
            if (line.StatusID != 1)
                return OperationResult.Fail("La línea de inventario no está activa.");

            var (applyError, _) = ApplyCore(line, type, quantity, referenceReason, referenceId, reason, user, now);
            return applyError is null ? OperationResult.Ok() : OperationResult.Fail(applyError);
        }

        // Núcleo compartido: calcula, valida las dos reglas de la tabla de efectos, modifica la línea y agrega la
        // transacción al contexto. No consulta ni guarda. Devuelve el error o la transacción creada.
        private (string? Error, InventoryTransaction? Transaction) ApplyCore(InventoryLine line, TransactionType type, int quantity,
            ReferenceType referenceReason, string? referenceId, string? reason, string user, DateTime now)
        {
            var effect = GetEffect(type, quantity);
            int qtyBefore = line.Quantity;
            int onHoldBefore = line.QuantityOnHold;
            int qtyAfter = qtyBefore + effect.Stock;
            int onHoldAfter = onHoldBefore + effect.OnHold;

            // Estas dos reglas cubren toda la tabla de efectos (son los mismos CHECK de la tabla):
            // liberar o vender más de lo reservado, y reservar o ajustar por encima del disponible.
            if (onHoldAfter < 0)
                return ($"No hay suficiente reservado. Solo hay {onHoldBefore} unidades reservadas.", null);
            if (onHoldAfter > qtyAfter)
                return ($"No hay suficiente disponible. Solo hay {line.QuantityAvailable} unidades disponibles.", null);

            line.Quantity = qtyAfter;
            line.QuantityOnHold = onHoldAfter;
            line.ModifiedBy = user;
            line.ModifiedOn = now;
            if (type is TransactionType.CargaInicial or TransactionType.IncrementoStock)
                line.LastRestock = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

            var transaction = new InventoryTransaction
            {
                InventoryLineID = line.InventoryLineID,
                Type = type,
                TransactionQuantity = quantity,
                QuantityBefore = qtyBefore,
                QuantityAfter = qtyAfter,
                OnHoldBefore = onHoldBefore,
                OnHoldAfter = onHoldAfter,
                ReferenceReason = referenceReason,
                ReferenceID = CleanText(referenceId),
                Reason = CleanText(reason),
                CreatedBy = user,
                CreatedOn = now
            };

            _inventoryTransactionRepository.Add(transaction);
            return (null, transaction);
        }

        // Validaciones que no necesitan la base de datos ni la línea. Se repiten aunque el DTO tenga atributos,
        // porque otros servicios (órdenes, importación) llaman aquí sin pasar por el model binding.
        private static string? ValidateMovement(TransactionType type, int quantity, string? referenceId, string? reason)
        {
            if (type == TransactionType.None || !Enum.IsDefined(type))
                return "El tipo de movimiento no es válido.";
            if (quantity <= 0)
                return "La cantidad debe ser mayor a cero.";
            if (type is TransactionType.AjustePositivo or TransactionType.AjusteNegativo && string.IsNullOrWhiteSpace(reason))
                return "Los ajustes requieren una razón.";
            if (type is TransactionType.ProductoReservado or TransactionType.ProductoLiberado or TransactionType.Venta && string.IsNullOrWhiteSpace(referenceId))
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
