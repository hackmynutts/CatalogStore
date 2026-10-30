using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;

namespace CatalogStore.BackendAPI.Repository.Orders
{
    public record OrderListRow(Order Order, int LineCount);
    // Una orden en revisión con lo necesario para cancelarla o avisarle a su vendedor (CreatedBy es el correo del vendedor).
    public record PendingOrderRow(int OrderID, string OrderNumber, string CreatedBy, string ClientName, decimal OrderTotalAmount, DateTime PendingSince);
    public interface IOrderRepository
    {
        Task<Order?> GetOrderByIdAsync(int orderId);
        Task<Order?> GetForUpdateAsync(int orderId);
        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<Order?> GetInProcessByClientForUpdateAsync(int clientId);
        Task<Order?> GetInProcessByClientAsync(int clientId);
        // Las órdenes que ya estaban en revisión antes de existir PendingSince quedan con la fecha vacía y ningún filtro las vería.
        // Les pone la fecha dada (la del primer arranque con esta versión): empiezan su plazo desde ahí y no se cancelan de golpe.
        // Devuelve cuántas actualizó. Es idempotente: sin fechas vacías no toca nada.
        Task<int> StampMissingPendingSinceAsync(DateTime nowUtc);
        // En revisión desde antes de cutoffUtc (ya vencidas), las más viejas primero.
        Task<List<PendingOrderRow>> GetExpiredPendingAsync(DateTime cutoffUtc, int take);
        // En revisión desde antes de warnCutoffUtc pero todavía no vencidas (desde expireCutoffUtc en adelante): les toca el aviso.
        Task<List<PendingOrderRow>> GetPendingToWarnAsync(DateTime warnCutoffUtc, DateTime expireCutoffUtc, int take);
        void AddOrder(Order order);
        void RemoveLine(OrderLine orderLine);
        Task<int> SaveChangesAsync();
        void DiscardChanges();
        Task<int> NextOrderNumberAsync();
        Task<List<OrderListRow>> GetListAsync(OrderStatus? status, int? clientId, string? createdBy);
    }
}
