using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Order;
using CatalogStore.BackendAPI.DTO.OrderLine;
using CatalogStore.BackendAPI.Models.Order;

namespace CatalogStore.BackendAPI.Services.Orders
{
    // Flujo: InProcess (se editan líneas, el stock queda reservado) → Pending (borrador bloqueado para validar)
    // → Completed (consume la reserva y luego genera la venta). Pending puede volver a InProcess; InProcess y Pending
    // se pueden cancelar (libera todo). Un cliente tiene como máximo una orden InProcess.
    //
    // Todas las escrituras devuelven OperationResult con Id = ID de la orden.
    // user e isAdmin los pasa el controller: el vendedor solo opera sus órdenes y tiene el tope de descuento.
    // Las lecturas devuelven null si la orden no existe o si es de otro vendedor (no se distingue a propósito).
    public interface IOrderServices
    {
        // Crear / abrir
        Task<OperationResult> StartAsync(int clientId, string user, bool isAdmin);
        Task<OperationResult> AddItemAsync(int clientId, AddOrderLineDTO dto, string user, bool isAdmin);

        // Editar (solo InProcess)
        Task<OperationResult> AddLineAsync(int orderId, AddOrderLineDTO dto, string user, bool isAdmin);
        Task<OperationResult> UpdateLineAsync(int orderId, int lineId, UpdateOrderLineDTO dto, string user, bool isAdmin);
        Task<OperationResult> RemoveLineAsync(int orderId, int lineId, string user, bool isAdmin);
        Task<OperationResult> UpdateHeaderAsync(int orderId, UpdateOrderDTO dto, string user, bool isAdmin);
        Task<OperationResult> SetApplyIvaAsync(int orderId, bool applyIva, string user, bool isAdmin);
        // Mismo % en todas las líneas (pisa los individuales; las líneas que se agreguen después no lo heredan).
        Task<OperationResult> SetDiscountAsync(int orderId, SetOrderDiscountDTO dto, string user, bool isAdmin);

        // Cambios de estado
        Task<OperationResult> SubmitAsync(int orderId, string user, bool isAdmin);
        Task<OperationResult> ReopenAsync(int orderId, string user, bool isAdmin);
        Task<OperationResult> CompleteAsync(int orderId, string user, bool isAdmin);
        Task<OperationResult> CancelAsync(int orderId, CancelOrderDTO dto, string user, bool isAdmin);

        // Lectura
        Task<OrderDTO?> GetAsync(int orderId, string user, bool isAdmin);
        Task<OrderDTO?> GetOpenByClientAsync(int clientId, string user, bool isAdmin);
        Task<List<OrderListItemDTO>> GetListAsync(OrderStatus? status, int? clientId, string user, bool isAdmin);
    }
}
