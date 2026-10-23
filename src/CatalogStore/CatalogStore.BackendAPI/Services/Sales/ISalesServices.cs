using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.SalesHeader;

namespace CatalogStore.BackendAPI.Services.Sales
{
    public interface ISalesServices
    {
        Task<SalesHeader> CreateFromOrderAsync(Order order, string createdBy, DateTime now);
    }
}
