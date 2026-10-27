using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.Invoicing
{
    public class InvoiceDTO
    {
        public int Id { get; set; }
        public int SalesHeaderID { get; set; }
        // Código de Hacienda: 1 factura, 3 nota de crédito, 4 tiquete.
        public int DocumentType { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? Clave { get; set; }
        public string? Consecutive { get; set; }
        public int PaymentMethod { get; set; }
        public int? ReferenceInvoiceID { get; set; }
        public DateTime IssuedOn { get; set; }
        public string? HaciendaStatus { get; set; }
        public string? HaciendaMessage { get; set; }
        public string? PdfUrl { get; set; }
        public string? PdfUrlTicket { get; set; }
        public string? Reason { get; set; }
        public bool HasXml { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }

    // Lo que ve el administrador antes de emitir. Nada de esto envía ni guarda nada.
    public class InvoicePreviewDTO
    {
        // true = se puede emitir ahora mismo.
        public bool CanEmit { get; set; }
        // Por qué no: datos que faltan o reglas (CABYS, totales, configuración...).
        public List<string> Errors { get; set; } = new();
        public int DocumentType { get; set; }
        public string DocumentTypeName { get; set; } = string.Empty;
        // Medio de pago que se usaría (el del primer abono) o null si hay que elegirlo.
        public int? PaymentMethod { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public int LineCount { get; set; }
        // Invoicing:Enabled. Con false la vista previa funciona pero no se puede emitir.
        public bool Enabled { get; set; }
        public List<string> MissingSettings { get; set; } = new();
    }

    public class EmitInvoiceDTO
    {
        // Si la venta ya tiene abonos se usa el del primero y este valor se ignora.
        public int? PaymentMethod { get; set; }
    }

    public class CreditNoteDTO
    {
        [Required(ErrorMessage = "Indique el motivo de la nota de crédito.")]
        [StringLength(180, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 180 caracteres.")]
        public string Reason { get; set; } = string.Empty;
    }

    public class DiscardInvoiceDTO
    {
        [Required(ErrorMessage = "Indique por qué se descarta el intento.")]
        [StringLength(250, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 250 caracteres.")]
        public string Reason { get; set; } = string.Empty;
    }
}
