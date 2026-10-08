using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Order
{
    public enum OrderStatus
    {
        [Display(Name = "En proceso")]
        InProcess= 0,
        [Display(Name = "Pendiente")]
        Pending = 1,
        [Display(Name = "Completada")]
        Completed = 2,
        [Display(Name = "Cancelada")]
        Cancelled = 3,
        [Display(Name = "Rechazada")]
        Rejected = 4,
        [Display(Name = "Retornada")]
        Returned = 5,
        [Display(Name = "Enviada")]
        Shipped = 6
    }
    [Table("Order_TB")]
    public class Order
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public decimal OrderTotalAmount { get; set; } = 0;
        public OrderStatus OrderStatus { get; set; } = OrderStatus.InProcess;
        public string? Notes { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
        public Models.Client.Client Client { get; set; } = null!;
        public ICollection<Models.OrderLine.OrderLine> OrderLines { get; set; } = new List<Models.OrderLine.OrderLine>();
    }
}
