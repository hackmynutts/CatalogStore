using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;

namespace CatalogStore.BackendAPI.Repository.Orders
{
    public record OrderListRow(Order Order, int LineCount);
    public interface IOrderRepository
    {
        Task<Order?> GetOrderByIdAsync(int orderId);
        Task<Order?> GetForUpdateAsync(int orderId);
        Task<Order?> GetOrderByNumberAsync(string orderNumber);
        Task<Order?> GetInProcessByClientForUpdateAsync(int clientId);
        Task<Order?> GetInProcessByClientAsync(int clientId);
        void AddOrder(Order order);
        void RemoveLine(OrderLine orderLine);
        Task<int> SaveChangesAsync();
        void DiscardChanges();
        Task<int> NextOrderNumberAsync();
        Task<List<OrderListRow>> GetListAsync(OrderStatus? status, int? clientId, string? createdBy);
    }
}
