using CatalogStore.BackendAPI.Models.Order;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Order
{
    // Fila del listado de órdenes. Sin líneas: el listado no las necesita y cargarlas para cada orden sería pesado.
    public class OrderListItemDTO
    {
        [Display(Name = "#")]
        public int OrderID { get; set; }
        [Display(Name = "Número de orden")]
        public string OrderNumber { get; set; } = string.Empty;
        [Display(Name = "Cliente")]
        public int ClientID { get; set; }
        [Display(Name = "Nombre del cliente")]
        public string ClientName { get; set; } = string.Empty;
        [Display(Name = "Estado")]
        public OrderStatus OrderStatus { get; set; } = OrderStatus.InProcess;
        [Display(Name = "Nombre del estado")]
        public string OrderStatusName { get; set; } = string.Empty;
        [Display(Name = "Monto total")]
        public decimal OrderTotalAmount { get; set; }
        [Display(Name = "Cantidad de productos")]
        public int LineCount { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
    }
}
