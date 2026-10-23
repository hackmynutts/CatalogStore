using CatalogStore.BackendAPI.Models.SalesHeader;

namespace CatalogStore.BackendAPI.Repository.Sales
{
    public interface ISalesRepository
    {
        void AddSale(SalesHeader sale);
        Task<int> NextSalesNumberAsync();
    }
}
