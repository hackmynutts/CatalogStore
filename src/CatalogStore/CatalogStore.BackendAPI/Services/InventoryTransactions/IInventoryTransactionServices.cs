using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.InventoryTransactions;
using CatalogStore.BackendAPI.Models.InventoryLine;
using CatalogStore.BackendAPI.Models.InventoryTransaction;

namespace CatalogStore.BackendAPI.Services.InventoryTransactions
{
    public interface IInventoryTransactionServices
    {
        Task<List<InventoryTransactionDTO>> GetByLineAsync(int inventoryLineId);
        // Movimiento suelto: carga la línea, lo aplica y guarda.
        Task<OperationResult> RegisterAsync(RegisterInventoryTransactionDTO dto, string createdBy);
        // Aplica el movimiento sobre una línea ya cargada con seguimiento, SIN guardar (para órdenes, que guardan una vez al final).
        OperationResult ApplyMovement(InventoryLine line, TransactionType type, int quantity,
            ReferenceType referenceReason, string? referenceId, string? reason, string user, DateTime now);
    }
}
