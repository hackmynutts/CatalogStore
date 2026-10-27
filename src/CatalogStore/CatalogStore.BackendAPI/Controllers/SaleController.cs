using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Sales;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Services.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    // Ventas: se crean al completar una orden (no hay POST para crearlas a mano). Aquí se consultan y se cobran.
    // Las reglas (vendedor solo ve/cobra sus ventas, solo admin anula abonos) viven en el servicio.
    // Escrituras: 200 { id } con el ID de la venta, 404 { message } si no existe o es de otro vendedor, 400 { message } por regla.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class SaleController : ControllerBase
    {
        private readonly ISalesServices _salesServices;
        public SaleController(ISalesServices salesServices)
        {
            _salesServices = salesServices;
        }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("AdminIT");

        // GET api/Sale?status=0&clientId=5&overdue=true
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] PaymentStatus? status, [FromQuery] int? clientId, [FromQuery] bool overdue = false) =>
            Ok(await _salesServices.GetListAsync(status, clientId, overdue, CurrentUser, IsAdmin));

        // GET api/Sale/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var sale = await _salesServices.GetAsync(id, CurrentUser, IsAdmin);
            return sale is null ? NotFound(new { message = SalesServices.NotFoundMessage }) : Ok(sale);
        }

        // GET api/Sale/by-order/12 — la venta que generó una orden completada (204 si la orden no tiene venta)
        [HttpGet("by-order/{orderId:int}")]
        public async Task<IActionResult> GetByOrder(int orderId)
        {
            var sale = await _salesServices.GetByOrderAsync(orderId, CurrentUser, IsAdmin);
            return sale is null ? NoContent() : Ok(sale);
        }

        // POST api/Sale/5/payments — registrar un abono
        [HttpPost("{id:int}/payments")]
        public async Task<IActionResult> RegisterPayment(int id, [FromBody] AddSalePaymentDTO dto) =>
            ToResponse(await _salesServices.RegisterPaymentAsync(id, dto, CurrentUser, IsAdmin));

        // POST api/Sale/5/payments/9/void — anular un abono (solo admin; lo valida el servicio)
        [HttpPost("{id:int}/payments/{paymentId:int}/void")]
        public async Task<IActionResult> VoidPayment(int id, int paymentId, [FromBody] VoidSalePaymentDTO dto) =>
            ToResponse(await _salesServices.VoidPaymentAsync(id, paymentId, dto, CurrentUser, IsAdmin));

        // POST api/Sale/5/void — anular la venta (solo admin, sin abonos vigentes; devuelve el stock y retorna la orden)
        [HttpPost("{id:int}/void")]
        public async Task<IActionResult> VoidSale(int id, [FromBody] VoidSaleDTO dto) =>
            ToResponse(await _salesServices.VoidSaleAsync(id, dto, CurrentUser, IsAdmin));

        private IActionResult ToResponse(OperationResult result)
        {
            if (result.Success)
                return Ok(new { id = result.Id });
            return result.Error == SalesServices.NotFoundMessage
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }
    }
}
