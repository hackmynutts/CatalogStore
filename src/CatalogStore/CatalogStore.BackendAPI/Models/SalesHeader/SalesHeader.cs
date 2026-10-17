using CatalogStore.BackendAPI.Models.Client;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.SalesHeader
{
    public enum PaymentStatus
    {
        [Display(Name = "Pendiente")]
        Pending = 0,
        [Display(Name = "Parcialmente Pagado")]
        PartiallyPaid = 1,
        [Display(Name = "Pagado")]
        Paid = 2,
        [Display(Name ="Anulada")]
        Voided = 3
    }
    [Table("SalesHeader_TB")]
    public class SalesHeader
    {
        public int SalesHeaderID { get; set; }
        public string SaleNumber { get; set; } = string.Empty;
        public int OrderID { get; set; } 
        public int ClientID { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientIdentification { get; set; } = string.Empty;
        public string? ClientCC { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public bool ApplyIva { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal IvaTotal { get; set; }
        public decimal TotalAmount { get; set; }
        public credit PaymentTerm { get; set; } = credit.D0;
        public DateOnly DueDate { get; set; }
        public decimal AmountPaid { get; set; } = 0;
        public decimal Balance { get; private set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        public string? Notes { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
        public Models.Client.Client Client { get; set; } = null!;
        public Models.Order.Order Order { get; set; } = null!;
        public ICollection<Models.SalesLine.SalesLine> Lines { get; set; } = new List<Models.SalesLine.SalesLine>();
    }
}
