using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Client;
using CatalogStore.UI.Models.Order;
using CatalogStore.UI.Models.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;

namespace CatalogStore.UI.Controllers
{
    // Páginas de órdenes (Index, Details) y puente entre el catálogo / el detalle (fetch desde order-catalog.js y
    // order-details.js) y api/Order. Las acciones POST devuelven JSON:
    // { success, message } o { success, orderId }. Los montos y las reglas (dueño, tope de descuento, stock) los decide
    // el backend; aquí solo se arma la llamada y se traduce el error a un mensaje legible.
    //
    // "Orden activa": la orden en proceso a la que el "+" del catálogo agrega productos. Se guarda en una cookie
    // propia con el usuario dueño, y SIEMPRE se valida contra la API antes de usarla (pudo completarse, cancelarse
    // o ser de otra sesión en el mismo navegador).
    [Authorize(Roles = "Admin,AdminIT,Vendedor")]
    public class OrderController : Controller
    {
        private const string ActiveOrderCookie = "cs_active_order";
        private readonly IHttpClientFactory _httpClientFactory;
        public OrderController(IHttpClientFactory httpClientFactory) { _httpClientFactory = httpClientFactory; }

        private string CurrentUser => User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value ?? string.Empty;

        // ───────── Páginas ─────────

        // GET: Order?status=1&clientId=5 — listado. El vendedor recibe solo sus órdenes (lo filtra la API).
        public async Task<IActionResult> Index(OrderStatus? status, int? clientId)
        {
            var query = new List<string>();
            if (status.HasValue) query.Add($"status={(int)status.Value}");
            if (clientId.HasValue) query.Add($"clientId={clientId.Value}");
            var url = "api/Order" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync(url);
            var orders = response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<OrderListItemViewModel>>() ?? new()
                : new List<OrderListItemViewModel>();

            ViewData["Status"] = status;
            ViewData["ClientId"] = clientId;
            ViewData["ClientName"] = clientId.HasValue ? orders.FirstOrDefault()?.ClientName : null;
            return View(orders);
        }

        // GET: Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Order/{id}");
            if (!response.IsSuccessStatusCode)
                return NotFound();

            var order = await response.Content.ReadFromJsonAsync<OrderViewModel>();
            if (order is null)
                return NotFound();

            ViewData["IsActiveOrder"] = ReadActiveOrderId() == order.OrderID;

            // Una orden completada tiene su venta: se enlaza desde la cabecera.
            if (order.OrderStatus is OrderStatus.Completed or OrderStatus.Returned)
            {
                var saleResponse = await client.GetAsync($"api/Sale/by-order/{order.OrderID}");
                if (saleResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    ViewData["Sale"] = await saleResponse.Content.ReadFromJsonAsync<SaleViewModel>();
            }
            return View(order);
        }

        // GET: Order/Proforma/5 — la cotización imprimible (no fiscal). Solo órdenes abiertas.
        public async Task<IActionResult> Proforma(int id)
        {
            var (order, clientInfo, notFound) = await LoadProformaAsync(id);
            if (notFound) return NotFound();
            if (order is null) return RedirectToAction(nameof(Details), new { id });
            ViewData["Client"] = clientInfo;
            return View(order);
        }

        // GET: Order/ProformaPdf/5 — la misma proforma como archivo PDF (Proforma-PRO-0000123.pdf)
        public async Task<IActionResult> ProformaPdf(int id, [FromServices] IWebHostEnvironment env, [FromServices] IConfiguration config)
        {
            var (order, clientInfo, notFound) = await LoadProformaAsync(id);
            if (notFound) return NotFound();
            if (order is null) return RedirectToAction(nameof(Details), new { id });

            // Sin logo (o sin carpeta wwwroot) el PDF sale igual, solo con el nombre de la empresa.
            var logoPath = env.WebRootPath is null ? null : Path.Combine(env.WebRootPath, "img", "logos", "logo_pequeño-removebg-preview.png");
            var contact = string.Join(" · ", new[] { config["Landing:WhatsApp"], config["Landing:Email"] }
                .Select(x => (x ?? string.Empty).Trim()).Where(x => x.Length > 0));
            var document = new CatalogStore.UI.Documents.ProformaPdfDocument(order, clientInfo, OrderDocs.LocalNow(), contact,
                logoPath is not null && System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null);
            return File(QuestPDF.Fluent.GenerateExtensions.GeneratePdf(document), "application/pdf", $"Proforma-{OrderDocs.ProformaNumber(order.OrderNumber)}.pdf");
        }

        // Orden abierta con productos + datos de contacto del cliente. notFound = la orden no existe o no es del vendedor;
        // order null (sin notFound) = existe pero ya no admite proforma (completada, cancelada o vacía).
        private async Task<(OrderViewModel? Order, CatalogStore.UI.Models.Client.ClientViewModel? Client, bool NotFound)> LoadProformaAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Order/{id}");
            if (!response.IsSuccessStatusCode)
                return (null, null, true);

            var order = await response.Content.ReadFromJsonAsync<OrderViewModel>();
            if (order is null)
                return (null, null, true);
            if (order.OrderStatus is not (OrderStatus.InProcess or OrderStatus.Pending) || order.Lines.Count == 0)
                return (null, null, false);

            // Datos de contacto del cliente: la orden solo guarda nombre e identificación.
            var clientResponse = await client.GetAsync($"api/Client/{order.ClientID}");
            var clientInfo = clientResponse.IsSuccessStatusCode
                ? await clientResponse.Content.ReadFromJsonAsync<CatalogStore.UI.Models.Client.ClientViewModel>()
                : null;
            return (order, clientInfo, false);
        }

        // POST: Order/ProformaEmail/5 — manda la proforma al correo del cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProformaEmail(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Order/{id}/proforma/email", null);
            return response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ApiErrorReader.ReadAsync(response, "No se pudo enviar la proforma.") });
        }

        // ───────── Acciones del detalle ─────────

        // POST: Order/Activate — "Agregar productos": deja la orden como activa y manda al catálogo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int orderId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Order/{orderId}");
            var order = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<OrderViewModel>() : null;
            if (order is null)
                return Json(new { success = false, message = "Orden no encontrada." });
            if (order.OrderStatus != OrderStatus.InProcess)
                return Json(new { success = false, message = "Solo se pueden agregar productos a una orden en proceso." });

            SetActiveOrder(order.OrderID);
            return Json(new { success = true, redirectUrl = Url.Action("Catalog", "Product") });
        }

        // POST: Order/UpdateNotes — reenvía el mismo cliente; la API solo cambia lo que difiere.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotes(int orderId, int clientId, string? notes)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Order/{orderId}", new { clientID = clientId, notes });
            return await ToJsonAsync(response, "No se pudieron guardar las notas.");
        }

        // POST: Order/Reopen — pendiente → en proceso
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(int orderId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Order/{orderId}/reopen", null);
            return await ToJsonAsync(response, "No se pudo devolver la orden a edición.");
        }

        // POST: Order/Complete — pendiente → completada (descuenta el inventario)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int orderId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Order/{orderId}/complete", null);
            if (!response.IsSuccessStatusCode)
                return await ToJsonAsync(response, "No se pudo completar la orden.");

            // Al completar nace la venta: se manda directo a ella para registrar el pago.
            var saleResponse = await client.GetAsync($"api/Sale/by-order/{orderId}");
            if (saleResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var sale = await saleResponse.Content.ReadFromJsonAsync<SaleViewModel>();
                if (sale is not null)
                    return Json(new { success = true, saleNumber = sale.SaleNumber, redirectUrl = Url.Action("Details", "Sales", new { id = sale.SalesHeaderID, created = true }) });
            }
            return Json(new { success = true });
        }

        // POST: Order/Cancel — libera lo reservado. Si era la orden activa, se suelta.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int orderId, string? reason)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync($"api/Order/{orderId}/cancel", new { reason = reason ?? string.Empty });
            if (response.IsSuccessStatusCode && ReadActiveOrderId() == orderId)
                ClearActiveOrder();
            return await ToJsonAsync(response, "No se pudo cancelar la orden.");
        }

        // ───────── Orden activa ─────────

        // GET: Order/Active — la orden activa con líneas y totales, o 204 si no hay (o ya no está en proceso).
        [HttpGet]
        public async Task<IActionResult> Active()
        {
            var order = await LoadActiveOrderAsync();
            return order is null ? NoContent() : Json(order);
        }

        // POST: Order/Release — "Soltar orden": deja de agregar a esa orden sin cancelarla (sigue en proceso).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Release()
        {
            ClearActiveOrder();
            return Json(new { success = true });
        }

        // GET: Order/Clients — clientes activos para el selector del catálogo.
        [HttpGet]
        public async Task<IActionResult> Clients()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/Client/active");
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = "No se pudo cargar la lista de clientes." });

            var clients = await response.Content.ReadFromJsonAsync<List<ClientViewModel>>() ?? new();
            return Json(new
            {
                success = true,
                clients = clients
                    .OrderBy(c => c.ClientName)
                    .Select(c => new { id = c.Id, name = c.ClientName, cc = c.CC, identification = c.Identification })
            });
        }

        // ───────── Crear / agregar ─────────

        // POST: Order/Start/5 — "Iniciar orden" desde el perfil del cliente. La deja activa y manda al catálogo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Order/start/{id}", null);
            if (!response.IsSuccessStatusCode)
                return Json(new { success = false, message = await ReadErrorAsync(response, "No se pudo iniciar la orden.") });

            var orderId = await ReadIdAsync(response);
            SetActiveOrder(orderId);
            return Json(new { success = true, orderId, redirectUrl = Url.Action("Catalog", "Product") });
        }

        // POST: Order/AddItem — "+" del catálogo. Con clientId agrega a la orden en proceso de ese cliente (o la crea);
        // sin clientId usa la orden activa. needsClient = true le indica al catálogo que pida el cliente.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int productId, int quantity = 1, int? clientId = null)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var body = new { productID = productId, quantity, discount = 0m };

            if (clientId.HasValue)
            {
                var created = await client.PostAsJsonAsync($"api/Order/add-item/{clientId.Value}", body);
                if (!created.IsSuccessStatusCode)
                    return Json(new { success = false, message = await ReadErrorAsync(created, "No se pudo agregar el producto.") });

                var newId = await ReadIdAsync(created);
                SetActiveOrder(newId);
                return Json(new { success = true, orderId = newId });
            }

            var activeId = ReadActiveOrderId();
            if (activeId is null)
                return Json(new { success = false, needsClient = true });

            var response = await client.PostAsJsonAsync($"api/Order/{activeId}/lines", body);
            if (response.IsSuccessStatusCode)
                return Json(new { success = true, orderId = activeId });

            // La orden activa pudo dejar de estar en proceso (otra pestaña, otro usuario): se suelta y se pide cliente.
            if (await LoadActiveOrderAsync() is null)
                return Json(new { success = false, needsClient = true, message = "La orden activa ya no está en proceso. Elegí el cliente para continuar." });

            return Json(new { success = false, message = await ReadErrorAsync(response, "No se pudo agregar el producto.") });
        }

        // ───────── Editar la orden activa ─────────

        // POST: Order/UpdateLine — cantidad. El descuento se reenvía tal cual (texto invariante: "5.5", no "5,5").
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLine(int orderId, int lineId, int quantity, string? discount)
        {
            if (!decimal.TryParse(discount ?? "0", NumberStyles.Number, CultureInfo.InvariantCulture, out var discountValue))
                return Json(new { success = false, message = "El descuento no es válido." });

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Order/{orderId}/lines/{lineId}", new { quantity, discount = discountValue });
            return await ToJsonAsync(response, "No se pudo actualizar la cantidad.");
        }

        // POST: Order/RemoveLine
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLine(int orderId, int lineId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.DeleteAsync($"api/Order/{orderId}/lines/{lineId}");
            return await ToJsonAsync(response, "No se pudo quitar el producto.");
        }

        // POST: Order/SetIva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetIva(int orderId, bool applyIva)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Order/{orderId}/iva", new { applyIva });
            return await ToJsonAsync(response, "No se pudo cambiar el IVA.");
        }

        // POST: Order/SetDiscount — el mismo % en todas las líneas (texto invariante: "5.5"). 0 = quitar descuentos.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDiscount(int orderId, string? discount)
        {
            if (!decimal.TryParse(discount ?? "0", NumberStyles.Number, CultureInfo.InvariantCulture, out var discountValue))
                return Json(new { success = false, message = "El descuento no es válido." });

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PutAsJsonAsync($"api/Order/{orderId}/discount", new { discount = discountValue });
            return await ToJsonAsync(response, "No se pudo aplicar el descuento.");
        }

        // POST: Order/Submit — en proceso → pendiente. La orden deja de estar activa: ya no se le agregan productos.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int orderId)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsync($"api/Order/{orderId}/submit", null);
            if (response.IsSuccessStatusCode && ReadActiveOrderId() == orderId)
                ClearActiveOrder();
            return await ToJsonAsync(response, "No se pudo enviar la orden a revisión.");
        }

        // ───────── Helpers ─────────

        private async Task<OrderViewModel?> LoadActiveOrderAsync()
        {
            var id = ReadActiveOrderId();
            if (id is null)
                return null;

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync($"api/Order/{id}");
            var order = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<OrderViewModel>() : null;
            if (order is null || order.OrderStatus != OrderStatus.InProcess)
            {
                ClearActiveOrder();
                return null;
            }
            return order;
        }

        // Valor "usuario|id": si en el mismo navegador entra otro usuario, la cookie del anterior se ignora.
        private int? ReadActiveOrderId()
        {
            var raw = Request.Cookies[ActiveOrderCookie];
            if (string.IsNullOrEmpty(raw))
                return null;
            var parts = raw.Split('|');
            if (parts.Length != 2 || parts[0] != CurrentUser || !int.TryParse(parts[1], out var id))
                return null;
            return id;
        }

        private void SetActiveOrder(int orderId) =>
            Response.Cookies.Append(ActiveOrderCookie, $"{CurrentUser}|{orderId}", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(7)
            });

        private void ClearActiveOrder() => Response.Cookies.Delete(ActiveOrderCookie);

        private static async Task<int> ReadIdAsync(HttpResponseMessage response)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetInt32();
        }

        private async Task<IActionResult> ToJsonAsync(HttpResponseMessage response, string fallback) =>
            response.IsSuccessStatusCode
                ? Json(new { success = true })
                : Json(new { success = false, message = await ReadErrorAsync(response, fallback) });

        // La API responde { message } en reglas de negocio y ProblemDetails { errors: { campo: [..] } } en validaciones.
        private static Task<string> ReadErrorAsync(HttpResponseMessage response, string fallback) =>
            ApiErrorReader.ReadAsync(response, fallback);
    }
}
