using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.OrderLine
{
    [Table("OrderLine_TB")]
    public class OrderLine
    {
        public int OrderLineID { get; set; }
        public int OrderID { get; set; }
        public int ProductID { get; set; }
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceIVA { get; set; }
        // Costo del producto al momento de completar la orden (null si el producto no tenía costo); sirve para calcular la ganancia de ventas viejas.
        public decimal? UnitCost { get; set; }
        public decimal LineTotalPrice { get; set; }
        public decimal Discount { get; set; } = 0.0m;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public Models.Order.Order Order { get; set; } = null!;
        public Models.Product.Product Product { get; set; } = null!;
    }
}
