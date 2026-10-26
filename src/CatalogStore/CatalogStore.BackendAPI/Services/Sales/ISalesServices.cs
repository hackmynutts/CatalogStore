using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Sales;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.SalesHeader;

namespace CatalogStore.BackendAPI.Services.Sales
{
    // Ventas: nacen al completar una orden (CreateFromOrderAsync, sin guardar) y después se cobran con abonos.
    // El vendedor ve y cobra solo sus ventas (Seller); el admin todas. Anular un abono es solo de admin.
    public interface ISalesServices
    {
        Task<SalesHeader> CreateFromOrderAsync(Order order, string createdBy, DateTime now);

        Task<SaleDTO?> GetAsync(int salesHeaderId, string user, bool isAdmin);
        Task<SaleDTO?> GetByOrderAsync(int orderId, string user, bool isAdmin);
        Task<List<SaleListItemDTO>> GetListAsync(PaymentStatus? status, int? clientId, bool overdueOnly, string user, bool isAdmin);

        Task<OperationResult> RegisterPaymentAsync(int salesHeaderId, AddSalePaymentDTO dto, string user, bool isAdmin);
        Task<OperationResult> VoidPaymentAsync(int salesHeaderId, int salesPaymentId, VoidSalePaymentDTO dto, string user, bool isAdmin);
    }
}
