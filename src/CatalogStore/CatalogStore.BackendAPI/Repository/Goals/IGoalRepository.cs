using CatalogStore.BackendAPI.Models.Goal;

namespace CatalogStore.BackendAPI.Repository.Goals
{
    // Avance de un vendedor en un rango de fechas. Montos sin IVA y sin redondear (el servicio redondea).
    public record SellerProgressRow(string Seller, decimal SoldAmount, int SalesCount, decimal CollectedAmount);

    // Una venta / un abono que suma en el avance (sin anuladas). Fechas en UTC.
    public record SaleDetailRow(int SalesHeaderID, string SaleNumber, DateTime SaleDate, string ClientName, bool ApplyIva,
        decimal Subtotal, decimal Total, int PaymentStatus);
    public record PaymentDetailRow(int SalesHeaderID, string SaleNumber, DateTime PaymentDate, string ClientName, int Method,
        decimal Amount, decimal CountedAmount);

    // Metas mensuales por vendedor y lo que lleva vendido / cobrado.
    public interface IGoalRepository
    {
        // ── Metas ──
        // Con seguimiento: para crear o actualizar la meta (RowVersion detecta ediciones simultáneas).
        Task<Goal?> GetAsync(string seller, int year, int month);
        // Sin seguimiento: para mostrar, no para editar.
        Task<Goal?> GetAsyncNoTracking(string seller, int year, int month);
        // Sin seguimiento: metas de todos los vendedores en un mes.
        Task<List<Goal>> GetByMonthAsync(int year, int month);
        // Sin seguimiento: metas de un vendedor entre dos meses. key = Year * 100 + Month (ej. 202610), ambos incluidos.
        Task<List<Goal>> GetBySellerRangeAsync(string seller, int fromKey, int toKey);
        void Add(Goal goal);
        // El RowVersion que quien edita vio al abrir la meta: si cambió desde entonces, el guardado falla por concurrencia.
        void SetExpectedRowVersion(Goal goal, byte[] rowVersion);
        Task<int> SaveChangesAsync();
        // Descarta los cambios pendientes antes de registrar un fallo en la bitácora.
        void DiscardChanges();

        // ── Avance ──
        // Vendido (Subtotal sin IVA) y cantidad por SaleDate; cobrado sin IVA por PaymentDate. Rango [fromUtc, toUtc).
        // Sin contar ventas anuladas ni abonos anulados. seller null = todos los vendedores con movimiento.
        Task<List<SellerProgressRow>> GetProgressAsync(DateTime fromUtc, DateTime toUtc, string? seller);

        // ── Detalle del avance ──
        // Las mismas filas que suma GetProgressAsync, una por una. Ventas por SaleDate, abonos por PaymentDate; sin anuladas.
        Task<List<SaleDetailRow>> GetSalesDetailAsync(DateTime fromUtc, DateTime toUtc, string seller);
        Task<List<PaymentDetailRow>> GetPaymentsDetailAsync(DateTime fromUtc, DateTime toUtc, string seller);
    }
}
