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
        // Misma definición de "vencida" que SalesServices.Overdue y GetListAsync: con saldo (Pendiente o Parcial) y vencimiento anterior a hoy.
        // Las anuladas y las pagadas no cuentan. Cuentan todas: de contado sin pagar, con o sin IVA.
        public Task<int> CountOverdueByClientAsync(int clientId, DateOnly today) =>
            _context.SalesHeaders.AsNoTracking()
                .CountAsync(s => s.ClientID == clientId
                                 && (s.PaymentStatus == PaymentStatus.Pending || s.PaymentStatus == PaymentStatus.PartiallyPaid)
                                 && s.DueDate < today);
        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
        public void DiscardChanges() => _context.ChangeTracker.Clear();

        public async Task<SalesHeader?> GetForExchangeAsync(int salesHeaderId) =>
            await _context.SalesHeaders
                .Include(s => s.Order)
                .Include(s => s.Lines)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);
        public void AddExchange(Models.SalesExchange.SalesExchange exchange) => _context.SalesExchanges.Add(exchange);

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
                .Include(s => s.Exchanges)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);

        public async Task<SalesHeader?> GetByOrderIdAsync(int orderId) =>
            await _context.SalesHeaders.AsNoTracking()
                .Include(s => s.Order)
                .Include(s => s.Lines)
                .Include(s => s.Payments)
                .Include(s => s.Exchanges)
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
