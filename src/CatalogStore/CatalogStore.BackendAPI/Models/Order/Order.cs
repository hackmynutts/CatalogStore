using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.Models.Order
{
    public enum OrderStatus
    {
        [Display(Name = "Pendiente")]
        Pending = 0,
        [Display(Name = "En proceso")]
        InProcess = 1,
        [Display(Name = "Completada")]
        Completed = 2,
        [Display(Name = "Cancelada")]
        Cancelled = 3
    }
    public class Order
    {
        public int OrderID { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int ClientID { get; set; }
        public decimal OrderTotalAmount { get; set; } = 0;
        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
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
