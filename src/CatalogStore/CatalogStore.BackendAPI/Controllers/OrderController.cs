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
    // Órdenes: los tres roles operan. Las reglas de quién puede qué (dueño de la orden, tope de descuento del vendedor)
    // viven en el servicio; aquí solo se arma user/isAdmin y se traduce el resultado a HTTP.
    // Escrituras: 200 { id } con el ID de la orden, 404 { message } si la orden no existe o es de otro vendedor,
    // o 400 { message } por una regla de negocio. Lecturas: 404 en los mismos casos.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderServices _orderServices;
        public OrderController(IOrderServices orderServices)
        {
            _orderServices = orderServices;
        }
        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? "Sistema";
        private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("AdminIT");

        // ───────── Lectura ─────────

        // GET api/Order?status=1&clientId=5 — el vendedor recibe solo sus órdenes
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] OrderStatus? status, [FromQuery] int? clientId) =>
            Ok(await _orderServices.GetListAsync(status, clientId, CurrentUser, IsAdmin));

        // GET api/Order/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var order = await _orderServices.GetAsync(id, CurrentUser, IsAdmin);
            return order is null ? NotFound(new { message = "Orden no encontrada." }) : Ok(order);
        }

        // GET api/Order/open/5 — orden en proceso del cliente (barra del catálogo, perfil del cliente).
        // 204 sin cuerpo cuando no tiene ninguna (o es de otro vendedor): no es un error.
        [HttpGet("open/{clientId:int}")]
        public async Task<IActionResult> GetOpen(int clientId)
        {
            var order = await _orderServices.GetOpenByClientAsync(clientId, CurrentUser, IsAdmin);
            return order is null ? NoContent() : Ok(order);
        }

        // ───────── Crear / agregar ─────────

        // POST api/Order/start/5 — "Iniciar orden" desde el cliente (devuelve la que ya tenga en proceso)
        [HttpPost("start/{clientId:int}")]
        public async Task<IActionResult> Start(int clientId) =>
            ToResponse(await _orderServices.StartAsync(clientId, CurrentUser, IsAdmin));

        // POST api/Order/add-item/5 — "+" del catálogo sin orden activa: busca o crea la orden del cliente
        [HttpPost("add-item/{clientId:int}")]
        public async Task<IActionResult> AddItem(int clientId, [FromBody] AddOrderLineDTO dto) =>
            ToResponse(await _orderServices.AddItemAsync(clientId, dto, CurrentUser, IsAdmin));

        // POST api/Order/12/lines — agregar a una orden conocida
        [HttpPost("{id:int}/lines")]
        public async Task<IActionResult> AddLine(int id, [FromBody] AddOrderLineDTO dto) =>
            ToResponse(await _orderServices.AddLineAsync(id, dto, CurrentUser, IsAdmin));

        // ───────── Editar (solo en proceso) ─────────

        // PUT api/Order/12/lines/30 — cantidad y descuento
        [HttpPut("{id:int}/lines/{lineId:int}")]
        public async Task<IActionResult> UpdateLine(int id, int lineId, [FromBody] UpdateOrderLineDTO dto) =>
            ToResponse(await _orderServices.UpdateLineAsync(id, lineId, dto, CurrentUser, IsAdmin));

        // DELETE api/Order/12/lines/30
        [HttpDelete("{id:int}/lines/{lineId:int}")]
        public async Task<IActionResult> RemoveLine(int id, int lineId) =>
            ToResponse(await _orderServices.RemoveLineAsync(id, lineId, CurrentUser, IsAdmin));

        // PUT api/Order/12 — cliente y notas
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateHeader(int id, [FromBody] UpdateOrderDTO dto) =>
            ToResponse(await _orderServices.UpdateHeaderAsync(id, dto, CurrentUser, IsAdmin));

        // PUT api/Order/12/iva — { "applyIva": false }
        [HttpPut("{id:int}/iva")]
        public async Task<IActionResult> SetApplyIva(int id, [FromBody] SetApplyIvaDTO dto) =>
            ToResponse(await _orderServices.SetApplyIvaAsync(id, dto.ApplyIva, CurrentUser, IsAdmin));

        // PUT api/Order/12/discount — { "discount": 5 } el mismo % en todas las líneas (0 = quitar descuentos)
        [HttpPut("{id:int}/discount")]
        public async Task<IActionResult> SetDiscount(int id, [FromBody] SetOrderDiscountDTO dto) =>
            ToResponse(await _orderServices.SetDiscountAsync(id, dto, CurrentUser, IsAdmin));

        // ───────── Cambios de estado ─────────

        // POST api/Order/12/submit — en proceso → pendiente
        [HttpPost("{id:int}/submit")]
        public async Task<IActionResult> Submit(int id) =>
            ToResponse(await _orderServices.SubmitAsync(id, CurrentUser, IsAdmin));

        // POST api/Order/12/reopen — pendiente → en proceso
        [HttpPost("{id:int}/reopen")]
        public async Task<IActionResult> Reopen(int id) =>
            ToResponse(await _orderServices.ReopenAsync(id, CurrentUser, IsAdmin));

        // POST api/Order/12/complete — pendiente → completada
        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id) =>
            ToResponse(await _orderServices.CompleteAsync(id, CurrentUser, IsAdmin));

        // POST api/Order/12/cancel — { "reason": "..." }
        [HttpPost("{id:int}/cancel")]
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
