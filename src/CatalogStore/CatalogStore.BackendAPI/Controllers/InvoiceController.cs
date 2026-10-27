using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Invoicing;
using CatalogStore.BackendAPI.Services.Invoicing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    // Facturación electrónica de las ventas. Emitir, anular, descartar y enviar son solo de administradores; el vendedor
    // solo consulta el estado de sus propias ventas. Errores: 400 { message }, 404 { message } si no existe.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceServices _invoiceServices;
        public InvoiceController(IInvoiceServices invoiceServices) { _invoiceServices = invoiceServices; }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("AdminIT");

        // GET api/Invoice/sale/5 — los comprobantes de una venta (factura o tiquete y su nota de crédito)
        [HttpGet("sale/{salesHeaderId:int}")]
        public async Task<IActionResult> GetBySale(int salesHeaderId)
        {
            var list = await _invoiceServices.GetBySaleAsync(salesHeaderId, CurrentUser, IsAdmin);
            return list is null ? NotFound(new { message = InvoiceServices.NotFoundMessage }) : Ok(list);
        }

        // GET api/Invoice/sale/5/preview?paymentMethod=1 — revisa todo sin enviar ni guardar nada
        [HttpGet("sale/{salesHeaderId:int}/preview")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Preview(int salesHeaderId, [FromQuery] int? paymentMethod)
        {
            var preview = await _invoiceServices.PreviewAsync(salesHeaderId, paymentMethod);
            return preview is null ? NotFound(new { message = InvoiceServices.NotFoundMessage }) : Ok(preview);
        }

        // POST api/Invoice/sale/5/emit — factura (con IVA) o tiquete (sin IVA). ¡Emite un comprobante REAL ante Hacienda!
        [HttpPost("sale/{salesHeaderId:int}/emit")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Emit(int salesHeaderId, [FromBody] EmitInvoiceDTO dto) =>
            ToResponse(await _invoiceServices.EmitAsync(salesHeaderId, dto, CurrentUser));

        // POST api/Invoice/sale/5/credit-note — anula por completo el comprobante aceptado de la venta
        [HttpPost("sale/{salesHeaderId:int}/credit-note")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> CreditNote(int salesHeaderId, [FromBody] CreditNoteDTO dto) =>
            ToResponse(await _invoiceServices.EmitCreditNoteAsync(salesHeaderId, dto, CurrentUser));

        // POST api/Invoice/9/refresh — consulta el estado en Hacienda y trae los enlaces del PDF
        [HttpPost("{id:int}/refresh")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Refresh(int id) =>
            ToResponse(await _invoiceServices.RefreshAsync(id, CurrentUser));

        // POST api/Invoice/9/discard — el intento "por conciliar" se verificó y NO se emitió
        [HttpPost("{id:int}/discard")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Discard(int id, [FromBody] DiscardInvoiceDTO dto) =>
            ToResponse(await _invoiceServices.DiscardAsync(id, dto, CurrentUser));

        // POST api/Invoice/9/email — manda el enlace del PDF al correo del cliente
        [HttpPost("{id:int}/email")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Email(int id) =>
            ToResponse(await _invoiceServices.SendEmailAsync(id, CurrentUser));

        // GET api/Invoice/9/xml — el XML firmado
        [HttpGet("{id:int}/xml")]
        [Authorize(Roles = "Admin,AdminIT")]
        public async Task<IActionResult> Xml(int id)
        {
            var xml = await _invoiceServices.GetXmlAsync(id);
            return xml is null ? NotFound(new { message = "Este comprobante no tiene XML guardado." }) : File(xml.Value.Bytes, "application/xml", xml.Value.FileName);
        }

        private IActionResult ToResponse(OperationResult result)
        {
            if (result.Success) return Ok(new { id = result.Id });
            return result.Error == InvoiceServices.NotFoundMessage
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }
    }
}
