namespace CatalogStore.UI.Models.Order
{
    // Estados de la orden, mismos números que el backend.
    public enum OrderStatus
    {
        InProcess = 0,
        Pending = 1,
        Completed = 2,
        Cancelled = 3,
        Rejected = 4,
        Returned = 5,
        Shipped = 6
    }

    // Clase CSS del badge de cada estado (theme.css → "Órdenes: listado y detalle"). Un solo lugar para todas las vistas.
    public static class OrderStatusStyle
    {
        public static string BadgeClass(OrderStatus status) => status switch
        {
            OrderStatus.InProcess => "badge-order badge-order-inprocess",
            OrderStatus.Pending => "badge-order badge-order-pending",
            OrderStatus.Completed => "badge-order badge-order-completed",
            OrderStatus.Cancelled or OrderStatus.Rejected => "badge-order badge-order-cancelled",
            OrderStatus.Returned => "badge-order badge-order-returned",
            _ => "badge-order"
        };
    }

    // Orden con sus líneas tal como la devuelve api/Order/{id}. Los montos ya vienen calculados por el backend;
    // la UI nunca recalcula precios. OrderStatusName ya viene en español.
    public class OrderViewModel
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientIdentification { get; set; } = string.Empty;
        public string? ClientCC { get; set; }
        public bool ApplyIva { get; set; } = true;
        public OrderStatus OrderStatus { get; set; }
        public string OrderStatusName { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal IvaTotal { get; set; }
        public decimal OrderTotalAmount { get; set; }
        public string? Notes { get; set; }
        public List<OrderLineViewModel> Lines { get; set; } = new();
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        // Solo en revisión: cuándo se cancela sola (UTC).
        public DateTime? ExpiresOn { get; set; }
    }

    // Fila del listado (api/Order). Sin líneas: solo la cantidad.
    public class OrderListItemViewModel
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientCC { get; set; }
        public bool ApplyIva { get; set; } = true;
        public OrderStatus OrderStatus { get; set; }
        public string OrderStatusName { get; set; } = string.Empty;
        public decimal OrderTotalAmount { get; set; }
        public int LineCount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }
    }

    // Texto y color del aviso "se cancela sola" de una orden en revisión.
    public static class OrderExpiry
    {
        // Días que faltan (redondeados hacia arriba). 0 = vence hoy; negativo = ya pasó y la próxima pasada la cancela.
        public static int DaysLeft(DateTime expiresOnUtc) => (int)Math.Ceiling((DateTime.SpecifyKind(expiresOnUtc, DateTimeKind.Utc) - DateTime.UtcNow).TotalDays);

        public static string Text(DateTime expiresOnUtc)
        {
            var left = DaysLeft(expiresOnUtc);
            return left <= 0 ? "Se cancela hoy" : left == 1 ? "Se cancela en 1 día" : $"Se cancela en {left} días";
        }

        // Rojo cuando faltan 2 días o menos; el texto siempre dice el plazo, el color solo refuerza.
        public static string CssClass(DateTime expiresOnUtc) => DaysLeft(expiresOnUtc) <= 2 ? "order-expiry is-soon" : "order-expiry";
    }

    public class OrderLineViewModel
    {
        public int OrderLineID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceIVA { get; set; }
        public decimal Discount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineIVA { get; set; }
        public decimal LineTotalPrice { get; set; }
    }
}
