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
        public async Task<SalesHeader?> GetForUpdateAsync(int salesHeaderId) =>
            await _context.SalesHeaders.Include(s => s.Payments).FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);
        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
        public void DiscardChanges() => _context.ChangeTracker.Clear();

        public async Task<SalesHeader?> GetForVoidAsync(int salesHeaderId) =>
            await _context.SalesHeaders
                .Include(s => s.Lines)
                .Include(s => s.Payments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);
        public async Task<SalesHeader?> GetByIdAsync(int salesHeaderId) =>
            await _context.SalesHeaders.AsNoTracking()
                .Include(s => s.Order)
                .Include(s => s.Lines)
                .Include(s => s.Payments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);

        public async Task<SalesHeader?> GetByOrderIdAsync(int orderId) =>
            await _context.SalesHeaders.AsNoTracking()
                .Include(s => s.Order)
                .Include(s => s.Lines)
                .Include(s => s.Payments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.OrderID == orderId);

        // overdueBefore: ventas con saldo (Pendiente o Parcial) cuyo vencimiento es anterior a esa fecha.
        public async Task<List<SalesListRow>> GetListAsync(PaymentStatus? status, int? clientId, string? seller, DateOnly? overdueBefore)
        {
            var query = _context.SalesHeaders.AsNoTracking();
            if (status.HasValue) query = query.Where(s => s.PaymentStatus == status.Value);
            if (clientId.HasValue) query = query.Where(s => s.ClientID == clientId.Value);
            if (seller != null) query = query.Where(s => s.Seller == seller);
            if (overdueBefore.HasValue)
                query = query.Where(s => (s.PaymentStatus == PaymentStatus.Pending || s.PaymentStatus == PaymentStatus.PartiallyPaid)
                                         && s.DueDate < overdueBefore.Value);

            return await query
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new SalesListRow(s, s.Lines.Count))
                .ToListAsync();
        }
    }
}
