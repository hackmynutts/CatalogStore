using CatalogStore.BackendAPI.DTO.OrderLine;
using CatalogStore.BackendAPI.Models.Order;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    public class OrderDTO
    {
        [Display(Name = "#")]
        public int OrderID { get; set; }
        [Display(Name = "Número de orden")]
        public string OrderNumber { get; set; } = string.Empty;
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }
        [Display(Name = "Nombre del cliente")]
        public string ClientName { get; set; } = string.Empty;
        [Display(Name = "Identificación del cliente")]
        public string ClientIdentification { get; set; } = string.Empty;
        [Display(Name = "Estado")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.InProcess;
        // Texto en español (Humanizer + [Display]); la UI no necesita conocer el enum.
        [Display(Name = "Nombre del estado")]
        public string OrderStatusName { get; set; } = string.Empty;
        // Suma de los LineSubtotal (sin IVA). Se calcula al mapear, no se guarda.
        [Display(Name = "Subtotal")]
        public decimal Subtotal { get; set; }
        // Suma de los LineIVA. Se calcula al mapear, no se guarda.
        [Display(Name = "IVA")]
        public decimal IvaTotal { get; set; }
        [Display(Name = "Monto total")]
        public decimal OrderTotalAmount { get; set; } = 0;
        [Display(Name = "Notas")]
        public string? Notes { get; set; }
        [Display(Name = "Productos")]
        public List<OrderLineDTO> Lines { get; set; } = new();
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
