namespace CatalogStore.UI.Models.Invoicing
{
    // Un comprobante electrónico de una venta, tal como lo devuelve api/Invoice/sale/{id}.
    public class InvoiceViewModel
    {
        public int Id { get; set; }
        public int SalesHeaderID { get; set; }
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

        public bool IsCreditNote => DocumentType == InvoiceUi.CreditNote;
        // Número individual de 10 dígitos (los últimos del consecutivo de 20).
        public string Number => Consecutive is { Length: 20 } ? Consecutive[10..] : "—";
    }

    // Los números tienen que coincidir con Models.Invoice del backend.
    public static class InvoiceUi
    {
        public const int CreditNote = 3;
        public const int Emitting = 0, Sent = 1, Accepted = 2, Rejected = 3, Uncertain = 4, Failed = 5;

        // Vigente: cuenta como "la venta ya tiene comprobante" (igual que InvoiceStates.Active del backend).
        public static bool IsActive(int status) => status is Emitting or Sent or Accepted or Uncertain;

        public static string StatusClass(int status) => status switch
        {
            Accepted => "badge-inv badge-inv-ok",
            Sent or Emitting => "badge-inv badge-inv-wait",
            Uncertain => "badge-inv badge-inv-warn",
            Rejected or Failed => "badge-inv badge-inv-bad",
            _ => "badge-inv"
        };

        // Un "enviando" de más de 2 minutos se considera trabado (el servidor usa la misma regla para permitir descartarlo).
        public static bool IsStuck(InvoiceViewModel i) => i.Status == Emitting && DateTime.UtcNow - DateTime.SpecifyKind(i.CreatedOn, DateTimeKind.Utc) > TimeSpan.FromMinutes(2);
    }
}
