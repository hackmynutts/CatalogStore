using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Invoicing;

namespace CatalogStore.BackendAPI.Services.Invoicing
{
    public interface IInvoiceServices
    {
        // null = la venta no existe o no es del vendedor. Un vendedor solo ve el estado de sus ventas.
        Task<List<InvoiceDTO>?> GetBySaleAsync(int salesHeaderId, string user, bool isAdmin);
        // Arma el JSON y revisa todo SIN enviar ni guardar. null = la venta no existe.
        Task<InvoicePreviewDTO?> PreviewAsync(int salesHeaderId, int? paymentMethod);
        // Emite la factura (con IVA) o el tiquete (sin IVA) de la venta. Id del resultado = id del comprobante.
        // Una sola emisión a la vez; el intento se guarda ANTES de enviar y nunca se reenvía solo.
        Task<OperationResult> EmitAsync(int salesHeaderId, EmitInvoiceDTO dto, string user);
        // Nota de crédito que anula por completo la factura o el tiquete aceptado de la venta.
        Task<OperationResult> EmitCreditNoteAsync(int salesHeaderId, CreditNoteDTO dto, string user);
        // Pregunta al proveedor el estado en Hacienda y trae los enlaces del PDF.
        Task<OperationResult> RefreshAsync(int invoiceId, string user);
        // Un intento "por conciliar" o trabado que el administrador verificó que NO se emitió: libera la venta.
        Task<OperationResult> DiscardAsync(int invoiceId, DiscardInvoiceDTO dto, string user);
        // Manda al cliente el enlace del PDF por correo (solo comprobantes aceptados).
        Task<OperationResult> SendEmailAsync(int invoiceId, string user);
        Task<(string FileName, byte[] Bytes)?> GetXmlAsync(int invoiceId);
    }
}
