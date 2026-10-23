using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.SalesHeader;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Sales
{
    public class SalesRepository : ISalesRepository
    {
        private readonly ApplicationDBContext _context;
        public SalesRepository(ApplicationDBContext context)
        {
            _context = context;
        }
        public void AddSale(SalesHeader sale) => _context.SalesHeaders.Add(sale);
        public async Task<int> NextSalesNumberAsync()
        {
            var next = new SqlParameter("@next", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync("SET @next = NEXT VALUE FOR dbo.SaleNumberSeq;", next);
            return (int)next.Value!;
        }
    }
}
