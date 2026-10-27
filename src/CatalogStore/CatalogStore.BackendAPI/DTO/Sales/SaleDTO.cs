using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Models.SalesPayment;

namespace CatalogStore.BackendAPI.DTO.Sales
{
    // Detalle de una venta: cabecera con la copia del cliente, líneas y abonos.
    // Los textos *Name vienen en español (Humanizer + [Display]); la UI no necesita conocer los enums.
    public class SaleDTO
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientIdentification { get; set; } = string.Empty;
        public IdentificationType ClientIdentificationType { get; set; }
        public string ClientIdentificationTypeName { get; set; } = string.Empty;
        public string? ClientCC { get; set; }
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public bool ApplyIva { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal IvaTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public credit PaymentTerm { get; set; }
        public string PaymentTermName { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string PaymentStatusName { get; set; } = string.Empty;
        // Con saldo y vencida a la fecha local de hoy.
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public string? Notes { get; set; }
        public string? VoidedBy { get; set; }
        public DateTime? VoidedOn { get; set; }
        public string? VoidedReason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public List<SaleLineDTO> Lines { get; set; } = new();
        public List<SalePaymentDTO> Payments { get; set; } = new();
    }

    public class SaleLineDTO
    {
        public int SalesLineID { get; set; }
        public int ProductID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceIVA { get; set; }
        public decimal Discount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineIva { get; set; }
        public decimal LineTotalPrice { get; set; }
    }

    public class SalePaymentDTO
    {
        public int SalesPaymentID { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string PaymentMethodName { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Notes { get; set; }
        public bool IsVoided { get; set; }
        public string? VoidedBy { get; set; }
        public DateTime? VoidedOn { get; set; }
        public string? VoidedReason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
