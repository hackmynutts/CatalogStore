using Microsoft.AspNetCore.RateLimiting;
using CatalogStore.BackendAPI.Services.Security;
using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Order;
using CatalogStore.BackendAPI.DTO.OrderLine;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Services.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace CatalogStore.BackendAPI.Controllers
{
    // Órdenes: Admin, AdminIT y Vendedor operan. El vendedor la crea (En proceso) y la manda a Pendiente; completarla (-> venta) es solo de Bodega, Admin y AdminIT.
    // Bodega ve solo las órdenes Pendientes (de todos los vendedores) y solo puede completar; no crea ni edita. Las reglas de quién puede qué (dueño de la orden, tope de descuento del vendedor)
    // viven en el servicio; aquí solo se arma user/isAdmin y se traduce el resultado a HTTP.
    // Escrituras: 200 { id } con el ID de la orden, 404 { message } si la orden no existe o es de otro vendedor,
    // o 400 { message } por una regla de negocio. Lecturas: 404 en los mismos casos.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor,Bodega")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _orderServices;
        public OrderController(IOrderServices orderServices)
        {
            _orderServices = orderServices;
        }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("AdminIT");
        // Bodega ve todas las órdenes (no solo las suyas) para poder despacharlas.
        private bool SeesAll => IsAdmin || User.IsInRole("Bodega");
        private const string NoBodega = "Admin,AdminIT,Vendedor";
        // Bodega solo trabaja las órdenes Pendientes (las que ya están listas para despachar); no ve las demás.
        private bool PendingOnly => !IsAdmin && !User.IsInRole("Vendedor") && User.IsInRole("Bodega");

        // POST api/Order/5/proforma/email — manda la proforma al correo del cliente (orden abierta; el vendedor, solo la suya)
        [HttpPost("{id:int}/proforma/email")]
        [EnableRateLimiting(RateLimitPolicies.Sensitive)]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> EmailProforma(int id, [FromServices] CatalogStore.BackendAPI.Services.Orders.IProformaServices proformaServices)
        {
            var result = await proformaServices.SendByEmailAsync(id, CurrentUser, IsAdmin);
            if (result.Success) return Ok(new { id = result.Id });
            return result.Error == OrderServices.NotFoundMessage
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        // ───────── Lectura ─────────

        // GET api/Order?status=1&clientId=5 — el vendedor recibe solo sus órdenes
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] OrderStatus? status, [FromQuery] int? clientId)
        {
            if (PendingOnly)
            {
                if (status.HasValue && status != OrderStatus.Pending)
                    return Ok(new List<OrderListItemDTO>());
                status = OrderStatus.Pending;
            }
            return Ok(await _orderServices.GetListAsync(status, clientId, CurrentUser, SeesAll));
        }

        // GET api/Order/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var order = await _orderServices.GetAsync(id, CurrentUser, SeesAll);
            if (order is null || (PendingOnly && order.OrderStatus != OrderStatus.Pending))
                return NotFound(new { message = "Orden no encontrada." });
            return Ok(order);
        }

        // GET api/Order/open/5 — orden en proceso del cliente (barra del catálogo, perfil del cliente).
        // 204 sin cuerpo cuando no tiene ninguna (o es de otro vendedor): no es un error.
        [HttpGet("open/{clientId:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> GetOpen(int clientId)
        {
            var order = await _orderServices.GetOpenByClientAsync(clientId, CurrentUser, IsAdmin);
            return order is null ? NoContent() : Ok(order);
        }

        // ───────── Crear / agregar ─────────

        // POST api/Order/start/5 — "Iniciar orden" desde el cliente (devuelve la que ya tenga en proceso). Cuerpo opcional { "notes": "..." }: solo se guarda si la orden es nueva.
        [HttpPost("start/{clientId:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> Start(int clientId, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] StartOrderDTO? dto = null) =>
            ToResponse(await _orderServices.StartAsync(clientId, CurrentUser, IsAdmin, dto?.Notes, dto?.Seller));

        // POST api/Order/add-item/5 — "+" del catálogo sin orden activa: busca o crea la orden del cliente
        [HttpPost("add-item/{clientId:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> AddItem(int clientId, [FromBody] AddOrderLineDTO dto) =>
            ToResponse(await _orderServices.AddItemAsync(clientId, dto, CurrentUser, IsAdmin));

        // POST api/Order/12/lines — agregar a una orden conocida
        [HttpPost("{id:int}/lines")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> AddLine(int id, [FromBody] AddOrderLineDTO dto) =>
            ToResponse(await _orderServices.AddLineAsync(id, dto, CurrentUser, IsAdmin));

        // ───────── Editar (solo en proceso) ─────────

        // PUT api/Order/12/lines/30 — cantidad y descuento
        [HttpPut("{id:int}/lines/{lineId:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> UpdateLine(int id, int lineId, [FromBody] UpdateOrderLineDTO dto) =>
            ToResponse(await _orderServices.UpdateLineAsync(id, lineId, dto, CurrentUser, IsAdmin));

        // DELETE api/Order/12/lines/30
        [HttpDelete("{id:int}/lines/{lineId:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> RemoveLine(int id, int lineId) =>
            ToResponse(await _orderServices.RemoveLineAsync(id, lineId, CurrentUser, IsAdmin));

        // PUT api/Order/12 — cliente y notas
        [HttpPut("{id:int}")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> UpdateHeader(int id, [FromBody] UpdateOrderDTO dto) =>
            ToResponse(await _orderServices.UpdateHeaderAsync(id, dto, CurrentUser, IsAdmin));

        // PUT api/Order/12/iva — { "applyIva": false }
        [HttpPut("{id:int}/iva")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> SetApplyIva(int id, [FromBody] SetApplyIvaDTO dto) =>
            ToResponse(await _orderServices.SetApplyIvaAsync(id, dto.ApplyIva, CurrentUser, IsAdmin));

        // PUT api/Order/12/discount — { "discount": 5 } el mismo % en todas las líneas (0 = quitar descuentos)
        [HttpPut("{id:int}/discount")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> SetDiscount(int id, [FromBody] SetOrderDiscountDTO dto) =>
            ToResponse(await _orderServices.SetDiscountAsync(id, dto, CurrentUser, IsAdmin));

        // ───────── Cambios de estado ─────────

        // POST api/Order/12/submit — en proceso → pendiente
        [HttpPost("{id:int}/submit")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> Submit(int id) =>
            ToResponse(await _orderServices.SubmitAsync(id, CurrentUser, IsAdmin));

        // POST api/Order/12/reopen — pendiente → en proceso
        [HttpPost("{id:int}/reopen")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> Reopen(int id) =>
            ToResponse(await _orderServices.ReopenAsync(id, CurrentUser, IsAdmin));

        // POST api/Order/12/complete — pendiente → completada. Solo Bodega o administración: el vendedor arma la orden y la envía a Pendiente, pero no la completa.
        [HttpPost("{id:int}/complete")]
        [Authorize(Roles = "Admin,AdminIT,Bodega")]
        public async Task<IActionResult> Complete(int id) =>
            ToResponse(await _orderServices.CompleteAsync(id, CurrentUser, SeesAll));

        // POST api/Order/12/cancel — { "reason": "..." }
        [HttpPost("{id:int}/cancel")]
        [Authorize(Roles = NoBodega)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelOrderDTO dto) =>
            ToResponse(await _orderServices.CancelAsync(id, dto, CurrentUser, IsAdmin));

        // "Orden no encontrada" (no existe o es de otro vendedor) es 404, igual que en las lecturas; el resto de reglas, 400.
        private IActionResult ToResponse(OperationResult result)
        {
            if (result.Success)
                return Ok(new { id = result.Id });
            return result.Error == OrderServices.NotFoundMessage
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }
    }
}
