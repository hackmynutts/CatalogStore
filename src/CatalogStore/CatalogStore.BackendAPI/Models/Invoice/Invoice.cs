using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Invoice
{
    // El número del enum ES el código de tipo de comprobante de Hacienda (01, 03, 04): ((int)tipo).ToString("D2").
    public enum InvoiceDocType
    {
        [Display(Name = "Factura electrónica")]
        Factura = 1,
        [Display(Name = "Nota de crédito")]
        NotaCredito = 3,
        // Reservado: hoy NO se emite. Las ventas sin IVA llevan un tiquete de caja no fiscal (decisión del negocio, 2026-10-27).
        [Display(Name = "Tiquete electrónico")]
        Tiquete = 4
    }

    public enum InvoiceStatus
    {
        // Se guardó el intento y todavía no se sabe si el proveedor lo recibió. Es lo primero que se escribe: si el
        // proceso se cae después de enviar, queda este rastro para conciliar.
        [Display(Name = "Enviando")]
        Emitting = 0,
        // El proveedor devolvió la clave: el comprobante existe y Hacienda lo está procesando.
        [Display(Name = "Enviada a Hacienda")]
        Sent = 1,
        [Display(Name = "Aceptada")]
        Accepted = 2,
        [Display(Name = "Rechazada")]
        Rejected = 3,
        // No se sabe si se emitió (tiempo agotado, respuesta ilegible). NUNCA se reenvía solo: un administrador concilia.
        [Display(Name = "Por conciliar")]
        Uncertain = 4,
        // Se sabe que NO se emitió (el proveedor rechazó la solicitud o nunca llegó). Se puede intentar de nuevo.
        [Display(Name = "No emitida")]
        Failed = 5
    }

    [Table("Invoice_TB")]
    public class Invoice
    {
        public int InvoiceID { get; set; }
        public int SalesHeaderID { get; set; }
        public InvoiceDocType DocumentType { get; set; }
        public InvoiceStatus Status { get; set; }
        // Código del medio de pago enviado (Hacienda: 01, 02, 03, 04, 06, 07, 99).
        public int PaymentMethod { get; set; }
        // Solo la nota de crédito: la factura o tiquete que anula.
        public int? ReferenceInvoiceID { get; set; }
        // Clave de 50 dígitos que devuelve el proveedor al emitir. Se guarda como texto: no es un número.
        public string? Clave { get; set; }
        // Consecutivo de 20 dígitos (sucursal + terminal + tipo + número), sacado de la clave.
        public string? Consecutive { get; set; }
        // Código de seguridad de 8 dígitos que forma parte de la clave; lo genera este sistema.
        public string SecurityCode { get; set; } = string.Empty;
        public DateTime IssuedOn { get; set; }
        // Exactamente lo que se envió al proveedor y lo que contestó: sin esto no se puede conciliar nada.
        public string RequestJson { get; set; } = string.Empty;
        public string? ResponseText { get; set; }
        public string? XmlBase64 { get; set; }
        public string? HaciendaStatus { get; set; }
        public string? HaciendaMessage { get; set; }
        public string? PdfUrl { get; set; }
        public string? PdfUrlTicket { get; set; }
        // Nota de crédito: motivo. Intento descartado: por qué se descartó.
        public string? Reason { get; set; }
        public DateTime? SentOn { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
        public Models.SalesHeader.SalesHeader SalesHeader { get; set; } = null!;
        public Invoice? ReferenceInvoice { get; set; }
    }
}
