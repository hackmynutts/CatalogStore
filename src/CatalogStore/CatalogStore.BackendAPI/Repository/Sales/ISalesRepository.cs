using CatalogStore.BackendAPI.Models.SalesHeader;

namespace CatalogStore.BackendAPI.Repository.Sales
{
    // Fila del listado de ventas: la cabecera sin líneas ni pagos, más cuántas líneas tiene.
    public record SalesListRow(SalesHeader Sale, int LineCount);

    // Repositorio del agregado Venta: cabecera + líneas + pagos se leen y se guardan juntos.
    public interface ISalesRepository
    {
        // ── Escritura ──
        void AddSale(SalesHeader sale);
        Task<int> NextSalesNumberAsync();
        // Con seguimiento y con los pagos (vigentes y anulados): para registrar o anular un abono.
        Task<SalesHeader?> GetForUpdateAsync(int salesHeaderId);
        // Ventas vencidas del cliente a la fecha dada (con saldo y vencimiento anterior a today): base del bloqueo por mora.
        Task<int> CountOverdueByClientAsync(int clientId, DateOnly today);
        // Con seguimiento, con líneas, su orden y los cambios anteriores: para registrar un cambio por producto defectuoso.
        Task<SalesHeader?> GetForExchangeAsync(int salesHeaderId);
        void AddExchange(Models.SalesExchange.SalesExchange exchange);
        // Con seguimiento, con líneas y pagos: para anular la venta y devolver el stock.
        Task<SalesHeader?> GetForVoidAsync(int salesHeaderId);
        Task<int> SaveChangesAsync();
        void DiscardChanges();

        // ── Lectura (sin seguimiento) ──
        Task<SalesHeader?> GetByIdAsync(int salesHeaderId);
        Task<SalesHeader?> GetByOrderIdAsync(int orderId);
        Task<List<SalesListRow>> GetListAsync(PaymentStatus? status, int? clientId, string? seller, DateOnly? overdueBefore);
    }
}
