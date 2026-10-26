using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.SalesPayment
{
    public enum PaymentMethod
    {
        [Display(Name ="Efectivo")]
        Efectivo = 1,
        [Display(Name ="Tarjeta")]
        Tarjeta = 2,
        [Display(Name ="Cheque")]
        Cheque =3,
        [Display(Name = "Transferencia / depósito")]
        Transferencia = 4,
        [Display(Name = "SINPE Móvil")]
        SinpeMovil = 6,
        [Display(Name = "Plataforma digital")]
        Plataforma = 7,
        [Display(Name = "Otros")]
        Otros = 99
    }
    [Table("SalesPayment_TB")]
    public class SalesPayment
    {
        public int SalesPaymentID { get; set; }
        public int SalesHeaderID { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? Notes { get; set; }
        public bool IsVoided { get; set; }
        public string? VoidedBy { get; set; }
        public DateTime? VoidedOn { get; set; }
        public string? VoidedReason { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public Models.SalesHeader.SalesHeader SalesHeader { get; set; } = null!;
    }
}
