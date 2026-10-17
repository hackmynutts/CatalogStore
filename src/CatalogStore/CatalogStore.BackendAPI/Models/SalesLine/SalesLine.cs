using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.SalesLine
{
    [Table("SalesLine_TB")]
    public class SalesLine
    {
        public int SalesLineID { get; set; }
        public int SalesHeaderID { get; set; }
        public int ProductID { get; set; }
        public string? ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitPriceIVA { get; set; }
        public decimal Discount { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal LineIva { get; set; }
        public decimal LineTotalPrice { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public Models.SalesHeader.SalesHeader SalesHeader { get; set; } = null!;
        public Models.Product.Product Product { get; set; } = null!;
        
    }
}
