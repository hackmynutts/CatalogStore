using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Orders
{
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public OrderRepository(ApplicationDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<Order?> GetOrderByIdAsync(int orderId) => await _dbContext.Orders.AsNoTracking().Include(o => o.OrderLines).Include(o => o.Client).FirstOrDefaultAsync(o => o.OrderID == orderId);
        public async Task<Order?> GetForUpdateAsync(int orderId) => await _dbContext.Orders.Include(o => o.OrderLines).FirstOrDefaultAsync(o => o.OrderID == orderId);
        public async Task<Order?> GetOrderByNumberAsync(string orderNumber) => await _dbContext.Orders.AsNoTracking().Include(o => o.OrderLines).Include(o => o.Client).FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        public async Task<Order?> GetInProcessByClientForUpdateAsync(int clientId) => await _dbContext.Orders.Include(o => o.OrderLines).FirstOrDefaultAsync(o => o.ClientID == clientId && o.OrderStatus == OrderStatus.InProcess);
        // Solo lectura (barra del catálogo / perfil del cliente): sin seguimiento y con el cliente para armar el DTO.
        public async Task<Order?> GetInProcessByClientAsync(int clientId) => await _dbContext.Orders.AsNoTracking().Include(o => o.OrderLines).Include(o => o.Client).FirstOrDefaultAsync(o => o.ClientID == clientId && o.OrderStatus == OrderStatus.InProcess);
        public void AddOrder(Order order) => _dbContext.Orders.Add(order);
        public void RemoveLine(OrderLine orderLine) => _dbContext.OrderLines.Remove(orderLine);
        public Task<int>SaveChangesAsync() => _dbContext.SaveChangesAsync();
        public void DiscardChanges() => _dbContext.ChangeTracker.Clear();
        public async Task<int> NextOrderNumberAsync()
        {
            var next = new SqlParameter("@next", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
            await _dbContext.Database.ExecuteSqlRawAsync("SET @next = NEXT VALUE FOR dbo.OrderNumberSeq;", next);
            return (int)next.Value!;
        }
        public async Task<List<OrderListRow>> GetListAsync(OrderStatus? status, int? clientId, string? createdBy)
        {
            var query = _dbContext.Orders.AsNoTracking();
            if (status.HasValue) query = query.Where(o => o.OrderStatus == status.Value);
            if (clientId.HasValue) query = query.Where(o => o.ClientID == clientId.Value);
            if (createdBy != null) query = query.Where(o => o.CreatedBy == createdBy);

            query = query.Include(o => o.Client).OrderByDescending(o => o.CreatedOn);
            var orders = query;
            return await orders.Select(o => new OrderListRow(o, o.OrderLines.Count)).ToListAsync();
        }
    }
}
