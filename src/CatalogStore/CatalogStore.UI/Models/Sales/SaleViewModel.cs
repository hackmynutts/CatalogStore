namespace CatalogStore.UI.Models.Sales
{
    // Mismos números que el backend (Models/SalesHeader/PaymentStatus).
    public enum PaymentStatus
    {
        Pending = 0,
        PartiallyPaid = 1,
        Paid = 2,
        Voided = 3
    }

    // Mismos números que el backend, que a su vez son los códigos de medio de pago de Hacienda.
    public enum PaymentMethod
    {
        Efectivo = 1,
        Tarjeta = 2,
        Cheque = 3,
        Transferencia = 4,
        SinpeMovil = 6,
        Plataforma = 7,
        Otros = 99
    }

    public static class SalesUi
    {
        // Opciones del selector de método de pago, en el orden en que más se usan.
        public static readonly (PaymentMethod Value, string Label)[] PaymentMethods =
        {
            (PaymentMethod.SinpeMovil, "SINPE Móvil"),
            (PaymentMethod.Efectivo, "Efectivo"),
            (PaymentMethod.Transferencia, "Transferencia / depósito"),
            (PaymentMethod.Tarjeta, "Tarjeta"),
            (PaymentMethod.Cheque, "Cheque"),
            (PaymentMethod.Plataforma, "Plataforma digital"),
            (PaymentMethod.Otros, "Otros")
        };

        // Clase CSS del badge de estado de pago (theme.css → "Ventas"). Un solo lugar para todas las vistas.
        public static string StatusBadge(PaymentStatus status) => status switch
        {
            PaymentStatus.Pending => "badge-pay badge-pay-pending",
            PaymentStatus.PartiallyPaid => "badge-pay badge-pay-partial",
            PaymentStatus.Paid => "badge-pay badge-pay-paid",
            _ => "badge-pay badge-pay-voided"
        };

        // Porcentaje cobrado (0–100) para la barra de progreso.
        public static int PaidPercent(decimal paid, decimal total) =>
            total <= 0 ? 0 : (int)Math.Clamp(Math.Floor(paid / total * 100), 0, 100);

        public static string Money(decimal value) => "₡" + value.ToString("N2");
    }

    // Detalle de una venta tal como lo devuelve api/Sale/{id}. Montos y estados vienen calculados del backend.
    public class SaleViewModel
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientIdentification { get; set; } = string.Empty;
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
        public string PaymentTermName { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public string PaymentStatusName { get; set; } = string.Empty;
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
        public string? Notes { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public List<SaleLineViewModel> Lines { get; set; } = new();
        public List<SalePaymentViewModel> Payments { get; set; } = new();
    }

    public class SaleLineViewModel
    {
        public int SalesLineID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineIva { get; set; }
        public decimal LineTotalPrice { get; set; }
    }

    public class SalePaymentViewModel
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

    // Fila del listado (api/Sale).
    public class SaleListItemViewModel
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
