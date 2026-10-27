using CatalogStore.BackendAPI.Data;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Models.SalesHeader;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Repository.Invoices
{
    // Estados que cuentan como "ya hay un comprobante": impiden emitir otro igual para la misma venta.
    public static class InvoiceStates
    {
        public static readonly InvoiceStatus[] Active =
            { InvoiceStatus.Emitting, InvoiceStatus.Sent, InvoiceStatus.Accepted, InvoiceStatus.Uncertain };
    }

    public interface IInvoiceRepository
    {
        // Con seguimiento: se agrega y se actualiza el comprobante durante la emisión.
        void Add(Invoice invoice);
        Task<Invoice?> GetForUpdateAsync(int invoiceId);
        Task<int> SaveChangesAsync();
        void DiscardChanges();

        // Sin seguimiento. Venta con líneas (y su producto: CABYS y unidad), abonos y cliente, lista para armar el comprobante.
        Task<SalesHeader?> GetSaleForInvoiceAsync(int salesHeaderId);
        Task<Invoice?> GetAsync(int invoiceId);
        Task<List<Invoice>> GetBySaleAsync(int salesHeaderId);
        // Actividad económica del cliente (Client.Cabys, nombre heredado), para el receptor.
        Task<string?> GetClientActivityAsync(int clientId);
        // El comprobante vigente (ver InvoiceStates.Active) de un tipo para una venta, si existe.
        Task<Invoice?> GetActiveAsync(int salesHeaderId, InvoiceDocType type);
    }

    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ApplicationDBContext _context;
        public InvoiceRepository(ApplicationDBContext context) { _context = context; }

        public void Add(Invoice invoice) => _context.Invoices.Add(invoice);
        public Task<Invoice?> GetForUpdateAsync(int invoiceId) => _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
        public void DiscardChanges() => _context.ChangeTracker.Clear();

        public Task<SalesHeader?> GetSaleForInvoiceAsync(int salesHeaderId) =>
            _context.SalesHeaders.AsNoTracking()
                .Include(s => s.Lines).ThenInclude(l => l.Product)
                .Include(s => s.Payments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(s => s.SalesHeaderID == salesHeaderId);

        public Task<Invoice?> GetAsync(int invoiceId) =>
            _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);

        public Task<List<Invoice>> GetBySaleAsync(int salesHeaderId) =>
            _context.Invoices.AsNoTracking().Where(i => i.SalesHeaderID == salesHeaderId).OrderBy(i => i.InvoiceID).ToListAsync();

        public Task<string?> GetClientActivityAsync(int clientId) =>
            _context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.Cabys).FirstOrDefaultAsync();

        public Task<Invoice?> GetActiveAsync(int salesHeaderId, InvoiceDocType type) =>
            _context.Invoices.AsNoTracking()
                .Where(i => i.SalesHeaderID == salesHeaderId && i.DocumentType == type && InvoiceStates.Active.Contains(i.Status))
                .FirstOrDefaultAsync();
    }
}
