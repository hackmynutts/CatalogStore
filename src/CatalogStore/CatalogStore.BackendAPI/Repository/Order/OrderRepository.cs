using CatalogStore.BackendAPI.Data;

namespace CatalogStore.BackendAPI.Repository.Order
{
    public class OrderRepository : IOrderRepository
    {
        private readonly ApplicationDBContext _dbContext;
        public OrderRepository(ApplicationDBContext dbContext)
        {
            _dbContext = dbContext;
        }
    }
}
