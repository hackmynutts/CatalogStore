using CatalogStore.BackendAPI.Models.SalesHeader;

namespace CatalogStore.BackendAPI.DTO.Sales
{
    // Fila del listado de ventas. Sin líneas ni pagos.
    public class SaleListItemDTO
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int OrderID { get; set; }
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientCC { get; set; }
        public string Seller { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public bool ApplyIva { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
        public string PaymentTermName { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string PaymentStatusName { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public int LineCount { get; set; }
    }
}
