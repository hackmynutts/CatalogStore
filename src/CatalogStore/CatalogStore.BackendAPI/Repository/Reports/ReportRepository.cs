using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Models.Product;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Models.SalesPayment;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Reports
{
    // Filas ya listas para los reportes. Las fechas llegan en UTC; el servicio las pasa a hora local.
    public record SaleReportRow(int SalesHeaderID, string SaleNumber, DateTime SaleDate, string Client, string Identification, string Seller,
        bool ApplyIva, decimal Subtotal, decimal Discount, decimal Iva, decimal Total, decimal Paid, decimal Balance,
        PaymentStatus Status, DateOnly DueDate, InvoiceStatus? InvoiceStatus, string? InvoiceNumber);

    public record PaymentReportRow(DateTime PaymentDate, string SaleNumber, string Client, string Seller, PaymentMethod Method,
        decimal Amount, string? Reference, string CreatedBy);

    // Costo: UnitCost de la línea de la orden × cantidad (costo histórico, el que tenía el producto al completar la orden).
    public record ProductSaleLine(int ProductID, string? Code, string Name, categoryType Category, int Quantity, decimal Sold, decimal Discount, decimal? Cost);

    public record StockReportRow(int ProductID, string? Code, string Name, categoryType Category, int Quantity, int OnHold, int Available,
        decimal? Price, decimal? Cost, StatusKind Status);

    public enum StatusKind { Active = 1, Inactive = 2 }

    public interface IReportRepository
    {
        // seller null = todos. includeVoided false = sin ventas anuladas.
        Task<List<SaleReportRow>> GetSalesAsync(DateTime fromUtc, DateTime toUtc, string? seller, bool includeVoided);
        // Ventas con saldo pendiente (pendientes o parciales).
        Task<List<SaleReportRow>> GetReceivablesAsync(string? seller);
        Task<List<PaymentReportRow>> GetPaymentsAsync(DateTime fromUtc, DateTime toUtc, string? seller);
        Task<List<ProductSaleLine>> GetProductSalesAsync(DateTime fromUtc, DateTime toUtc);
        Task<List<StockReportRow>> GetStockAsync();
    }

    public class ReportRepository : IReportRepository
    {
        private readonly ApplicationDBContext _context;
        public ReportRepository(ApplicationDBContext context) { _context = context; }

        public async Task<List<SaleReportRow>> GetSalesAsync(DateTime fromUtc, DateTime toUtc, string? seller, bool includeVoided)
        {
            var query = _context.SalesHeaders.AsNoTracking().Where(s => s.SaleDate >= fromUtc && s.SaleDate < toUtc);
            if (!includeVoided) query = query.Where(s => s.PaymentStatus != PaymentStatus.Voided);
            if (seller is not null) query = query.Where(s => s.Seller == seller);
            return await Project(query.OrderBy(s => s.SaleDate));
        }

        public async Task<List<SaleReportRow>> GetReceivablesAsync(string? seller)
        {
            var query = _context.SalesHeaders.AsNoTracking()
                .Where(s => s.PaymentStatus == PaymentStatus.Pending || s.PaymentStatus == PaymentStatus.PartiallyPaid);
            if (seller is not null) query = query.Where(s => s.Seller == seller);
            return await Project(query.OrderBy(s => s.DueDate).ThenBy(s => s.SaleDate));
        }

        // Una consulta para las ventas y otra para sus facturas vigentes (la factura de cada venta, si la tiene).
        private async Task<List<SaleReportRow>> Project(IQueryable<SalesHeader> query)
        {
            var sales = await query.Select(s => new
            {
                s.SalesHeaderID, s.SaleNumber, s.SaleDate, s.ClientName, s.ClientIdentification, s.Seller, s.ApplyIva,
                s.Subtotal, s.DiscountTotal, s.IvaTotal, s.TotalAmount, s.AmountPaid, s.PaymentStatus, s.DueDate
            }).ToListAsync();

            var ids = sales.Select(s => s.SalesHeaderID).ToList();
            var invoices = ids.Count == 0
                ? new Dictionary<int, Invoice>()
                : (await _context.Invoices.AsNoTracking()
                        .Where(i => ids.Contains(i.SalesHeaderID) && i.DocumentType == InvoiceDocType.Factura
                                    && (i.Status == InvoiceStatus.Emitting || i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.Accepted || i.Status == InvoiceStatus.Uncertain))
                        .ToListAsync())
                    .GroupBy(i => i.SalesHeaderID).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.InvoiceID).First());

            return sales.Select(s =>
            {
                invoices.TryGetValue(s.SalesHeaderID, out var inv);
                // Una venta anulada no cobra: su saldo es 0.
                var balance = s.PaymentStatus == PaymentStatus.Voided ? 0m : s.TotalAmount - s.AmountPaid;
                return new SaleReportRow(s.SalesHeaderID, s.SaleNumber, s.SaleDate, s.ClientName, s.ClientIdentification, s.Seller, s.ApplyIva,
                    s.Subtotal, s.DiscountTotal, s.IvaTotal, s.TotalAmount, s.AmountPaid, balance, s.PaymentStatus, s.DueDate,
                    inv?.Status, inv?.Consecutive is { Length: 20 } c ? c[10..] : null);
            }).ToList();
        }

        public Task<List<PaymentReportRow>> GetPaymentsAsync(DateTime fromUtc, DateTime toUtc, string? seller)
        {
            var query = _context.SalesPayments.AsNoTracking()
                .Where(p => !p.IsVoided && p.SalesHeader.PaymentStatus != PaymentStatus.Voided && p.PaymentDate >= fromUtc && p.PaymentDate < toUtc);
            if (seller is not null) query = query.Where(p => p.SalesHeader.Seller == seller);
            return query.OrderBy(p => p.PaymentDate).ThenBy(p => p.SalesPaymentID)
                .Select(p => new PaymentReportRow(p.PaymentDate, p.SalesHeader.SaleNumber, p.SalesHeader.ClientName, p.SalesHeader.Seller,
                    p.PaymentMethod, p.Amount, p.Reference, p.CreatedBy))
                .ToListAsync();
        }

        public async Task<List<ProductSaleLine>> GetProductSalesAsync(DateTime fromUtc, DateTime toUtc)
        {
            // El costo viene de la línea de la orden (UnitCost): el costo que tenía el producto cuando se completó la orden.
            var lines = await (from l in _context.SalesLines.AsNoTracking()
                               join s in _context.SalesHeaders on l.SalesHeaderID equals s.SalesHeaderID
                               join ol in _context.OrderLines on new { s.OrderID, l.ProductID } equals new { ol.OrderID, ol.ProductID } into olj
                               from ol in olj.DefaultIfEmpty()
                               where s.PaymentStatus != PaymentStatus.Voided && s.SaleDate >= fromUtc && s.SaleDate < toUtc
                               select new
                               {
                                   l.ProductID, l.ProductCode, l.ProductName, Category = l.Product.categoria, l.Quantity,
                                   Sold = l.LineSubtotal, Discount = l.UnitPrice * l.Quantity - l.LineSubtotal,
                                   Cost = ol.UnitCost == null ? (decimal?)null : ol.UnitCost * l.Quantity
                               }).ToListAsync();

            // Si alguna línea de un producto no tiene costo, el costo y la utilidad de ese producto quedan sin calcular
            // en lugar de mostrar una ganancia inflada.
            return lines.GroupBy(l => l.ProductID).Select(g =>
            {
                var first = g.First();
                return new ProductSaleLine(g.Key, g.Select(x => x.ProductCode).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)), first.ProductName, first.Category,
                    g.Sum(x => x.Quantity), g.Sum(x => x.Sold), g.Sum(x => x.Discount),
                    g.All(x => x.Cost is not null) ? g.Sum(x => x.Cost!.Value) : null);
            }).OrderByDescending(p => p.Sold).ToList();
        }

        public async Task<List<StockReportRow>> GetStockAsync()
        {
            var rows = await _context.InventoryLines.AsNoTracking()
                .Select(i => new
                {
                    i.ProductID, i.Product.ProductCode, i.Product.ProductName, i.Product.categoria, i.Quantity, i.QuantityOnHold,
                    i.Product.Price, i.Product.CostPrice, i.Product.StatusID
                }).ToListAsync();
            return rows.GroupBy(r => r.ProductID).Select(g =>
            {
                var f = g.First();
                var qty = g.Sum(x => x.Quantity); var hold = g.Sum(x => x.QuantityOnHold);
                return new StockReportRow(g.Key, f.ProductCode, f.ProductName, f.categoria, qty, hold, qty - hold, f.Price, f.CostPrice,
                    f.StatusID == 1 ? StatusKind.Active : StatusKind.Inactive);
            }).OrderBy(r => r.Name).ToList();
        }
    }
}
