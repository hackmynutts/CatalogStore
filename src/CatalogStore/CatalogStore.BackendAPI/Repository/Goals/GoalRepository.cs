using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.Goal;
using CatalogStore.BackendAPI.Models.SalesHeader;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Goals
{
    public class GoalRepository : IGoalRepository
    {
        private readonly ApplicationDBContext _context;

        public GoalRepository(ApplicationDBContext context)
        {
            _context = context;
        }

        // ── Metas ──

        public Task<Goal?> GetAsync(string seller, int year, int month) =>
            _context.Goals.FirstOrDefaultAsync(g => g.Seller == seller && g.Year == year && g.Month == month);

        public Task<Goal?> GetAsyncNoTracking(string seller, int year, int month) =>
            _context.Goals.AsNoTracking().FirstOrDefaultAsync(g => g.Seller == seller && g.Year == year && g.Month == month);

        public Task<List<Goal>> GetByMonthAsync(int year, int month) =>
            _context.Goals.AsNoTracking()
                .Where(g => g.Year == year && g.Month == month)
                .OrderBy(g => g.Seller)
                .ToListAsync();

        public Task<List<Goal>> GetBySellerRangeAsync(string seller, int fromKey, int toKey) =>
            _context.Goals.AsNoTracking()
                .Where(g => g.Seller == seller
                         && g.Year * 100 + g.Month >= fromKey
                         && g.Year * 100 + g.Month <= toKey)
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

        public void Add(Goal goal) => _context.Goals.Add(goal);

        public void SetExpectedRowVersion(Goal goal, byte[] rowVersion) =>
            _context.Entry(goal).Property(g => g.RowVersion).OriginalValue = rowVersion;

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

        public void DiscardChanges() => _context.ChangeTracker.Clear();

        // ── Avance ──

        public async Task<List<SellerProgressRow>> GetProgressAsync(DateTime fromUtc, DateTime toUtc, string? seller)
        {
            // Una fila por vendedor; SQL Server agrupa y suma (usa IX_SalesHeader_Seller_SaleDate).
            var sales = await _context.SalesHeaders
                .Where(s => s.PaymentStatus != PaymentStatus.Voided
                         && s.SaleDate >= fromUtc && s.SaleDate < toUtc
                         && (seller == null || s.Seller == seller))
                .GroupBy(s => s.Seller)
                .Select(g => new { Seller = g.Key, Sold = g.Sum(s => s.Subtotal), Count = g.Count() })
                .ToListAsync();

            // Cobrado sin IVA: cada abono por la proporción sin IVA de su venta. Se multiplica antes de dividir
            // para que SQL Server no redondee la proporción; TotalAmount 0 no debería existir, pero no tumba la consulta.
            var collected = await _context.SalesPayments
                .Where(p => !p.IsVoided
                         && p.PaymentDate >= fromUtc && p.PaymentDate < toUtc
                         && p.SalesHeader.PaymentStatus != PaymentStatus.Voided
                         && (seller == null || p.SalesHeader.Seller == seller))
                .GroupBy(p => p.SalesHeader.Seller)
                .Select(g => new
                {
                    Seller = g.Key,
                    Collected = g.Sum(p => p.SalesHeader.TotalAmount == 0
                        ? 0m
                        : p.Amount * p.SalesHeader.Subtotal / p.SalesHeader.TotalAmount)
                })
                .ToListAsync();

            // Unión por vendedor: puede tener ventas sin cobros en el mes o cobros sin ventas (cobra lo de otro mes).
            var soldBySeller = sales.ToDictionary(s => s.Seller);
            var collectedBySeller = collected.ToDictionary(c => c.Seller);

            return soldBySeller.Keys.Union(collectedBySeller.Keys)
                .OrderBy(name => name)
                .Select(name => new SellerProgressRow(
                    name,
                    soldBySeller.TryGetValue(name, out var s) ? s.Sold : 0m,
                    soldBySeller.TryGetValue(name, out var c) ? c.Count : 0,
                    collectedBySeller.TryGetValue(name, out var p) ? p.Collected : 0m))
                .ToList();
        }
    }
}
