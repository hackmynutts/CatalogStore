using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Order;
using CatalogStore.BackendAPI.DTO.OrderLine;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryLine;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Repository.Orders;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.InventoryTransactions;
using Humanizer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Orders
{
    /// <summary>
    /// Flujo de una orden: InProcess (se agregan/quitan productos, el stock queda reservado) → Pending (borrador
    /// bloqueado para validar; los precios se recalculan al entrar) → Completed (consume la reserva con Venta).
    /// Pending puede volver a InProcess. InProcess y Pending se pueden cancelar (libera todo lo reservado).
    ///
    /// Reglas de todas las escrituras: se carga la orden con seguimiento, se valida dueño y estado, se aplican los
    /// movimientos de stock sin guardar, y se guarda UNA vez al final. Si algo falla después de aplicar un movimiento
    /// se descarta todo (DiscardChanges) antes de responder o registrar en la bitácora.
    /// </summary>
    public class OrderServices : IOrderServices
    {
        private const string ModuleName = "Ventas/Órdenes";
        private const string OrdersTable = "Order";
        // Público: el controller lo usa para responder 404 en vez de 400.
        public const string NotFoundMessage = "Orden no encontrada.";
        private readonly IOrderRepository _orderRepository;
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IInventoryTransactionServices _inventoryTransactionServices;
        private readonly IProductRepository _productRepository;
        private readonly IClientServices _clientServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly IOptions<PricingOptions> _pricing;
        private readonly IOptions<OrdersOptions> _ordersOptions;
        private readonly TimeProvider _time;
        public OrderServices(IOrderRepository orderRepository, IInventoryLineRepository inventoryLineRepository, IInventoryTransactionServices inventoryTransactionServices, IProductRepository productRepository, IClientServices clientServices, IEventlogServices eventlogServices, IOptions<PricingOptions> pricingOptions, IOptions<OrdersOptions> ordersOptions, TimeProvider time)
        {
            _orderRepository = orderRepository;
            _inventoryLineRepository = inventoryLineRepository;
            _inventoryTransactionServices = inventoryTransactionServices;
            _productRepository = productRepository;
            _clientServices = clientServices;
            _eventlogServices = eventlogServices;
            _pricing = pricingOptions;
            _ordersOptions = ordersOptions;
            _time = time;
        }

        // ───────────────────────── Crear / abrir ─────────────────────────

        // Botón "Iniciar orden" del perfil del cliente: si ya tiene una en proceso la devuelve, si no crea una vacía.
        public Task<OperationResult> StartAsync(int clientId, string user, bool isAdmin) =>
            WithRetryAsync(() => StartCoreAsync(clientId, user, isAdmin), clientId.ToString(), "iniciar una orden");

        private async Task<OperationResult> StartCoreAsync(int clientId, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var client = await _clientServices.GetClientAsync(clientId);
            if (client == null || client.StatusID != 1)
                return OperationResult.Fail("El cliente no existe o no está activo.");

            var existing = await _orderRepository.GetInProcessByClientForUpdateAsync(clientId);
            if (existing is not null)
                return CanAccess(existing, user, isAdmin)
                    ? OperationResult.Ok(existing.OrderID)
                    : OperationResult.Fail(InProcessByOtherMessage(existing));

            var order = await NewOrderAsync(clientId, user, now);
            await _orderRepository.SaveChangesAsync();
            await LogOrderCreatedAsync(order);
            return OperationResult.Ok(order.OrderID);
        }

        // ───────────────────────── Agregar productos ─────────────────────────

        // "+" del catálogo sin orden activa: busca la orden InProcess del cliente o la crea, y agrega la línea.
        public Task<OperationResult> AddItemAsync(int clientId, AddOrderLineDTO dto, string user, bool isAdmin) =>
            WithRetryAsync(() => AddItemCoreAsync(clientId, dto, user, isAdmin), clientId.ToString(), "agregar un producto a la orden");

        // "+" con orden activa, o agregar desde la pantalla de la orden: la orden ya se conoce.
        public Task<OperationResult> AddLineAsync(int orderId, AddOrderLineDTO dto, string user, bool isAdmin) =>
            WithRetryAsync(() => AddLineCoreAsync(orderId, dto, user, isAdmin), orderId.ToString(), "agregar un producto a la orden");

        private async Task<OperationResult> AddItemCoreAsync(int clientId, AddOrderLineDTO dto, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var client = await _clientServices.GetClientAsync(clientId);
            if (client == null || client.StatusID != 1)
                return OperationResult.Fail("El cliente no existe o no está activo.");

            var (error, product, inventoryLine) = await ValidateItemAsync(dto, isAdmin);
            if (error is not null)
                return OperationResult.Fail(error);

            var order = await _orderRepository.GetInProcessByClientForUpdateAsync(clientId);
            var isNewOrder = order is null;
            if (order is null)
                order = await NewOrderAsync(clientId, user, now);
            else if (!CanAccess(order, user, isAdmin))
                return OperationResult.Fail(InProcessByOtherMessage(order));

            var result = AddOrIncrementLine(order, product!, inventoryLine!, dto, user, now, isNewOrder);
            if (!result.Success)
                return result;

            await _orderRepository.SaveChangesAsync();

            if (isNewOrder)
                await LogOrderCreatedAsync(order);

            return OperationResult.Ok(order.OrderID);
        }

        private async Task<OperationResult> AddLineCoreAsync(int orderId, AddOrderLineDTO dto, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;

            // Primero la orden: si no existe o no está en proceso no tiene sentido validar el producto.
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var (error, product, inventoryLine) = await ValidateItemAsync(dto, isAdmin);
            if (error is not null)
                return OperationResult.Fail(error);

            var result = AddOrIncrementLine(order, product!, inventoryLine!, dto, user, now, isNewOrder: false);
            if (!result.Success)
                return result;

            await _orderRepository.SaveChangesAsync();
            return OperationResult.Ok(order.OrderID);
        }

        // Validaciones comunes de un producto a agregar: tope de descuento, producto vendible y línea de inventario.
        // La línea de inventario vuelve con seguimiento porque ApplyMovement le cambia el reservado.
        private async Task<(string? Error, Models.Product.Product? Product, InventoryLine? InventoryLine)> ValidateItemAsync(AddOrderLineDTO dto, bool isAdmin)
        {
            var maxDiscount = _ordersOptions.Value.MaxSellerDiscountPercent;
            if (!isAdmin && dto.Discount > maxDiscount)
                return ($"El descuento máximo permitido para vendedores es {maxDiscount} %.", null, null);

            var products = await _productRepository.GetByIdsAsync(new[] { dto.ProductID });
            if (!products.TryGetValue(dto.ProductID, out var product) || product.StatusID != 1)
                return ("El producto no existe o no está activo.", null, null);
            if (product.Price is null or <= 0)
                return ($"{product.ProductCode}: no tiene precio, no se puede vender.", null, null);

            var inventoryLines = await _inventoryLineRepository.GetForUpdateByProductsAsync(new[] { dto.ProductID });
            if (!inventoryLines.TryGetValue(dto.ProductID, out var inventoryLine) || inventoryLine.StatusID != 1)
                return ($"{product.ProductCode}: no está en el inventario o su línea está inactiva.", null, null);

            return (null, product, inventoryLine);
        }

        // Reserva la cantidad y crea la línea, o suma a la existente. No guarda.
        // Si la reserva falla descarta TODO lo pendiente (orden nueva incluida) antes de devolver el error.
        private OperationResult AddOrIncrementLine(Order order, Models.Product.Product product, InventoryLine inventoryLine,
            AddOrderLineDTO dto, string user, DateTime now, bool isNewOrder)
        {
            var move = _inventoryTransactionServices.ApplyMovement(inventoryLine, TransactionType.ProductoReservado, dto.Quantity,
                ReferenceType.Orden, order.OrderNumber, null, user, now);
            if (!move.Success)
            {
                _orderRepository.DiscardChanges();
                return OperationResult.Fail($"{product.ProductCode}: {move.Error}");
            }

            var line = order.OrderLines.FirstOrDefault(l => l.ProductID == product.ProductID);
            if (line is null)
            {
                // Línea nueva: copia código, nombre y precio del producto. El descuento del DTO solo se usa aquí.
                // OrderID no se asigna: EF lo completa al guardar porque la línea cuelga de la orden.
                line = new OrderLine
                {
                    ProductID = product.ProductID,
                    ProductCode = product.ProductCode,
                    ProductName = product.ProductName,
                    Quantity = dto.Quantity,
                    UnitPrice = product.Price!.Value,
                    Discount = dto.Discount,
                    CreatedBy = user,
                    CreatedOn = now
                };
                order.OrderLines.Add(line);
            }
            else
            {
                // Ya estaba: solo suma cantidad. Conserva el descuento y el precio copiados al crearla.
                line.Quantity += dto.Quantity;
                line.ModifiedBy = user;
                line.ModifiedOn = now;
            }

            RecalculateLine(line, IvaRate(order));
            RecalculateTotal(order);
            if (!isNewOrder)
                Touch(order, user, now);
            return OperationResult.Ok(order.OrderID);
        }

        // ───────────────────────── Editar (solo InProcess) ─────────────────────────

        // Cambia cantidad y descuento. Reserva o libera solo la diferencia de cantidad.
        public Task<OperationResult> UpdateLineAsync(int orderId, int lineId, UpdateOrderLineDTO dto, string user, bool isAdmin) =>
            WithRetryAsync(() => UpdateLineCoreAsync(orderId, lineId, dto, user, isAdmin), orderId.ToString(), "modificar una línea de la orden");

        private async Task<OperationResult> UpdateLineCoreAsync(int orderId, int lineId, UpdateOrderLineDTO dto, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var line = order.OrderLines.FirstOrDefault(l => l.OrderLineID == lineId);
            if (line is null)
                return OperationResult.Fail("La línea no pertenece a esta orden.");

            // El tope solo se aplica si el vendedor CAMBIA el descuento: así puede editar la cantidad de una línea
            // a la que un administrador le dio más del 5 %.
            var maxDiscount = _ordersOptions.Value.MaxSellerDiscountPercent;
            if (!isAdmin && dto.Discount > maxDiscount && dto.Discount != line.Discount)
                return OperationResult.Fail($"El descuento máximo permitido para vendedores es {maxDiscount} %.");

            var diff = dto.Quantity - line.Quantity;
            if (diff == 0 && dto.Discount == line.Discount)
                return OperationResult.Ok(order.OrderID);

            if (diff != 0)
            {
                var type = diff > 0 ? TransactionType.ProductoReservado : TransactionType.ProductoLiberado;
                var moveError = await ApplyToLinesAsync(order, new[] { (line, Math.Abs(diff)) }, type, null, user, now);
                if (moveError is not null)
                {
                    _orderRepository.DiscardChanges();
                    return OperationResult.Fail(moveError);
                }
            }

            line.Quantity = dto.Quantity;
            line.Discount = dto.Discount;
            line.ModifiedBy = user;
            line.ModifiedOn = now;
            RecalculateLine(line, IvaRate(order));
            RecalculateTotal(order);
            Touch(order, user, now);

            await _orderRepository.SaveChangesAsync();
            return OperationResult.Ok(order.OrderID);
        }

        // Quita la línea y libera todo lo que tenía reservado.
        public Task<OperationResult> RemoveLineAsync(int orderId, int lineId, string user, bool isAdmin) =>
            WithRetryAsync(() => RemoveLineCoreAsync(orderId, lineId, user, isAdmin), orderId.ToString(), "quitar una línea de la orden");

        private async Task<OperationResult> RemoveLineCoreAsync(int orderId, int lineId, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var line = order.OrderLines.FirstOrDefault(l => l.OrderLineID == lineId);
            if (line is null)
                return OperationResult.Fail("La línea no pertenece a esta orden.");

            var moveError = await ApplyToLinesAsync(order, new[] { (line, line.Quantity) }, TransactionType.ProductoLiberado, null, user, now);
            if (moveError is not null)
            {
                _orderRepository.DiscardChanges();
                return OperationResult.Fail(moveError);
            }

            _orderRepository.RemoveLine(line);
            RecalculateTotal(order, excluded: line);
            Touch(order, user, now);

            await _orderRepository.SaveChangesAsync();
            return OperationResult.Ok(order.OrderID);
        }

        // Notas y cliente. El cliente solo cambia si el nuevo no tiene otra orden en proceso (una por cliente).
        public Task<OperationResult> UpdateHeaderAsync(int orderId, UpdateOrderDTO dto, string user, bool isAdmin) =>
            WithRetryAsync(() => UpdateHeaderCoreAsync(orderId, dto, user, isAdmin), orderId.ToString(), "editar la orden");

        private async Task<OperationResult> UpdateHeaderCoreAsync(int orderId, UpdateOrderDTO dto, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            if (dto.ClientID == order.ClientID && notes == order.Notes)
                return OperationResult.Ok(order.OrderID);

            var before = new { order.ClientID, order.Notes };

            if (dto.ClientID != order.ClientID)
            {
                var client = await _clientServices.GetClientAsync(dto.ClientID);
                if (client == null || client.StatusID != 1)
                    return OperationResult.Fail("El cliente no existe o no está activo.");

                var other = await _orderRepository.GetInProcessByClientForUpdateAsync(dto.ClientID);
                if (other is not null)
                    return OperationResult.Fail($"{client.ClientName} ya tiene la orden {other.OrderNumber} en proceso.");

                order.ClientID = dto.ClientID;
            }

            order.Notes = notes;
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"Orden {order.OrderNumber}: datos generales actualizados.",
                preData: before, postData: new { order.ClientID, order.Notes });
            return OperationResult.Ok(order.OrderID);
        }

        // Orden con o sin IVA. Recalcula todas las líneas con la tarifa nueva.
        public Task<OperationResult> SetApplyIvaAsync(int orderId, bool applyIva, string user, bool isAdmin) =>
            WithRetryAsync(() => SetApplyIvaCoreAsync(orderId, applyIva, user, isAdmin), orderId.ToString(), "cambiar el IVA de la orden");

        private async Task<OperationResult> SetApplyIvaCoreAsync(int orderId, bool applyIva, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);
            if (order.ApplyIva == applyIva)
                return OperationResult.Ok(order.OrderID);

            var totalBefore = order.OrderTotalAmount;
            order.ApplyIva = applyIva;
            var rate = IvaRate(order);
            foreach (var line in order.OrderLines)
                RecalculateLine(line, rate);
            RecalculateTotal(order);
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"Orden {order.OrderNumber}: {(applyIva ? "se aplica IVA" : "sin IVA")}.",
                preData: new { ApplyIva = !applyIva, OrderTotalAmount = totalBefore },
                postData: new { order.ApplyIva, order.OrderTotalAmount });
            return OperationResult.Ok(order.OrderID);
        }

        // ───────────────────────── Cambios de estado ─────────────────────────

        // InProcess → Pending. Recalcula los precios con los datos actuales de los productos:
        // lo que se valida en Pending es lo que se va a cobrar.
        public Task<OperationResult> SubmitAsync(int orderId, string user, bool isAdmin) =>
            WithRetryAsync(() => SubmitCoreAsync(orderId, user, isAdmin), orderId.ToString(), "enviar la orden a revisión");

        private async Task<OperationResult> SubmitCoreAsync(int orderId, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadForEditAsync(orderId, user, isAdmin);
            if (order is null)
                return OperationResult.Fail(loadError!);
            if (order.OrderLines.Count == 0)
                return OperationResult.Fail("La orden no tiene productos.");

            var (products, productError) = await LoadSellableProductsAsync(order);
            if (products is null)
                return OperationResult.Fail(productError!);

            var rate = IvaRate(order);
            foreach (var line in order.OrderLines)
            {
                var product = products[line.ProductID];
                if (line.UnitPrice != product.Price!.Value || line.ProductCode != product.ProductCode || line.ProductName != product.ProductName)
                {
                    line.UnitPrice = product.Price.Value;
                    line.ProductCode = product.ProductCode;
                    line.ProductName = product.ProductName;
                    line.ModifiedBy = user;
                    line.ModifiedOn = now;
                }
                RecalculateLine(line, rate);
            }
            RecalculateTotal(order);
            order.OrderStatus = OrderStatus.Pending;
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await LogStatusChangeAsync(typeEvent.Edit, order, OrderStatus.InProcess, "enviada a revisión");
            return OperationResult.Ok(order.OrderID);
        }

        // Pending → InProcess, para volver a editar. Respeta la regla de una sola orden en proceso por cliente.
        public Task<OperationResult> ReopenAsync(int orderId, string user, bool isAdmin) =>
            WithRetryAsync(() => ReopenCoreAsync(orderId, user, isAdmin), orderId.ToString(), "devolver la orden a edición");

        private async Task<OperationResult> ReopenCoreAsync(int orderId, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadAsync(orderId, user, isAdmin, OrderStatus.Pending);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var other = await _orderRepository.GetInProcessByClientForUpdateAsync(order.ClientID);
            if (other is not null)
                return OperationResult.Fail($"El cliente ya tiene la orden {other.OrderNumber} en proceso. Completala o cancelala antes de reabrir esta.");

            order.OrderStatus = OrderStatus.InProcess;
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await LogStatusChangeAsync(typeEvent.Edit, order, OrderStatus.Pending, "devuelta a edición");
            return OperationResult.Ok(order.OrderID);
        }

        // Pending → Completed. Vuelve a revisar los productos; si un precio cambió desde que se envió a revisión
        // NO completa y devuelve las diferencias. Consume la reserva con un movimiento Venta por línea y guarda el costo.
        public Task<OperationResult> CompleteAsync(int orderId, string user, bool isAdmin) =>
            WithRetryAsync(() => CompleteCoreAsync(orderId, user, isAdmin), orderId.ToString(), "completar la orden", typeEvent.CompleteFail);

        private async Task<OperationResult> CompleteCoreAsync(int orderId, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadAsync(orderId, user, isAdmin, OrderStatus.Pending);
            if (order is null)
                return OperationResult.Fail(loadError!);
            if (order.OrderLines.Count == 0)
                return OperationResult.Fail("La orden no tiene productos.");

            var (products, productError) = await LoadSellableProductsAsync(order);
            if (products is null)
                return await CompleteFailAsync(order, productError!);

            var changes = order.OrderLines
                .Where(l => l.UnitPrice != products[l.ProductID].Price!.Value)
                .Select(l => $"{l.ProductCode}: {l.UnitPrice:N2} → {products[l.ProductID].Price!.Value:N2}")
                .ToList();
            if (changes.Count > 0)
                return await CompleteFailAsync(order,
                    $"Los precios cambiaron desde que se envió a revisión ({string.Join("; ", changes)}). Devolvé la orden a edición y enviala de nuevo.");

            var moveError = await ApplyToLinesAsync(order, order.OrderLines.Select(l => (l, l.Quantity)), TransactionType.Venta, null, user, now);
            if (moveError is not null)
            {
                _orderRepository.DiscardChanges();
                return await CompleteFailAsync(order, moveError);
            }

            foreach (var line in order.OrderLines)
                line.UnitCost = products[line.ProductID].CostPrice;

            order.OrderStatus = OrderStatus.Completed;
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await LogStatusChangeAsync(typeEvent.Complete, order, OrderStatus.Pending, "completada");
            return OperationResult.Ok(order.OrderID);
        }

        // InProcess o Pending → Cancelled. Libera todo lo reservado; el motivo queda en cada movimiento y en la bitácora.
        public Task<OperationResult> CancelAsync(int orderId, CancelOrderDTO dto, string user, bool isAdmin) =>
            WithRetryAsync(() => CancelCoreAsync(orderId, dto, user, isAdmin), orderId.ToString(), "cancelar la orden", typeEvent.CancelFail);

        private async Task<OperationResult> CancelCoreAsync(int orderId, CancelOrderDTO dto, string user, bool isAdmin)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var (order, loadError) = await LoadAsync(orderId, user, isAdmin, OrderStatus.InProcess, OrderStatus.Pending);
            if (order is null)
                return OperationResult.Fail(loadError!);

            var reason = dto.Reason.Trim();
            var previous = order.OrderStatus;
            var moveError = await ApplyToLinesAsync(order, order.OrderLines.Select(l => (l, l.Quantity)), TransactionType.ProductoLiberado, reason, user, now);
            if (moveError is not null)
            {
                _orderRepository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.CancelFail, ModuleName, OrdersTable, order.OrderID.ToString(),
                    $"No se pudo cancelar la orden {order.OrderNumber}: {moveError}");
                return OperationResult.Fail(moveError);
            }

            order.OrderStatus = OrderStatus.Cancelled;
            Touch(order, user, now);
            await _orderRepository.SaveChangesAsync();

            await _eventlogServices.LogAsync(typeEvent.Cancel, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"Orden {order.OrderNumber} cancelada. Motivo: {reason}",
                preData: new { OrderStatus = previous },
                postData: new { order.OrderStatus, Reason = reason, order.OrderTotalAmount, Lines = order.OrderLines.Count });
            return OperationResult.Ok(order.OrderID);
        }

        // ───────────────────────── Lectura ─────────────────────────

        public async Task<OrderDTO?> GetAsync(int orderId, string user, bool isAdmin)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            return order is null || !CanAccess(order, user, isAdmin) ? null : MapOrder(order);
        }

        // Para la barra del catálogo y el perfil del cliente. null si no hay o si es de otro vendedor.
        public async Task<OrderDTO?> GetOpenByClientAsync(int clientId, string user, bool isAdmin)
        {
            var order = await _orderRepository.GetInProcessByClientAsync(clientId);
            return order is null || !CanAccess(order, user, isAdmin) ? null : MapOrder(order);
        }

        // El vendedor siempre ve solo sus órdenes, aunque no lo pida.
        public async Task<List<OrderListItemDTO>> GetListAsync(OrderStatus? status, int? clientId, string user, bool isAdmin)
        {
            var rows = await _orderRepository.GetListAsync(status, clientId, isAdmin ? null : user);
            return rows.Select(r => new OrderListItemDTO
            {
                OrderID = r.Order.OrderID,
                OrderNumber = r.Order.OrderNumber,
                ClientID = r.Order.ClientID,
                ClientName = r.Order.Client?.ClientName ?? string.Empty,
                ClientCC = r.Order.Client?.CC,
                ApplyIva = r.Order.ApplyIva,
                OrderStatus = r.Order.OrderStatus,
                OrderStatusName = r.Order.OrderStatus.Humanize(),
                OrderTotalAmount = r.Order.OrderTotalAmount,
                LineCount = r.LineCount,
                CreatedBy = r.Order.CreatedBy,
                CreatedOn = r.Order.CreatedOn
            }).ToList();
        }

        private static OrderDTO MapOrder(Order order)
        {
            var lines = order.OrderLines
                .OrderBy(l => l.OrderLineID)
                .Select(l =>
                {
                    // El IVA de la línea sale de lo guardado (total − subtotal): coincide siempre con LineTotalPrice.
                    var subtotal = OrderPricing.LineSubtotal(l.UnitPrice, l.Quantity, l.Discount);
                    return new OrderLineDTO
                    {
                        OrderLineID = l.OrderLineID,
                        OrderID = l.OrderID,
                        ProductID = l.ProductID,
                        ProductCode = l.ProductCode,
                        ProductName = l.ProductName,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        UnitPriceIVA = l.UnitPriceIVA,
                        Discount = l.Discount,
                        LineSubtotal = subtotal,
                        LineIVA = l.LineTotalPrice - subtotal,
                        LineTotalPrice = l.LineTotalPrice,
                        CreatedBy = l.CreatedBy,
                        CreatedOn = l.CreatedOn,
                        ModifiedBy = l.ModifiedBy,
                        ModifiedOn = l.ModifiedOn
                    };
                })
                .ToList();

            return new OrderDTO
            {
                OrderID = order.OrderID,
                OrderNumber = order.OrderNumber,
                ClientID = order.ClientID,
                ClientName = order.Client?.ClientName ?? string.Empty,
                ClientIdentification = order.Client?.Identification ?? string.Empty,
                ClientCC = order.Client?.CC,
                ApplyIva = order.ApplyIva,
                OrderStatus = order.OrderStatus,
                OrderStatusName = order.OrderStatus.Humanize(),
                Subtotal = lines.Sum(l => l.LineSubtotal),
                IvaTotal = lines.Sum(l => l.LineIVA),
                OrderTotalAmount = order.OrderTotalAmount,
                Notes = order.Notes,
                Lines = lines,
                CreatedBy = order.CreatedBy,
                CreatedOn = order.CreatedOn,
                ModifiedBy = order.ModifiedBy,
                ModifiedOn = order.ModifiedOn
            };
        }

        // ───────────────────────── Helpers ─────────────────────────

        // Admin opera cualquier orden; el vendedor solo las que creó.
        private static bool CanAccess(Order order, string user, bool isAdmin) => isAdmin || order.CreatedBy == user;

        private static string InProcessByOtherMessage(Order order) =>
            $"El cliente tiene la orden {order.OrderNumber} en proceso, creada por {order.CreatedBy}.";

        // Carga con seguimiento y valida dueño y estado. "Orden no encontrada" también para órdenes ajenas,
        // así un vendedor no puede saber si un ID existe.
        private async Task<(Order? Order, string? Error)> LoadAsync(int orderId, string user, bool isAdmin, params OrderStatus[] allowed)
        {
            var order = await _orderRepository.GetForUpdateAsync(orderId);
            if (order is null || !CanAccess(order, user, isAdmin))
                return (null, NotFoundMessage);
            if (!allowed.Contains(order.OrderStatus))
                return (null, $"La orden {order.OrderNumber} está {order.OrderStatus.Humanize().ToLower()} y no permite esta acción.");
            return (order, null);
        }

        // Las ediciones (líneas, cabecera, IVA, enviar a revisión) solo se permiten en proceso.
        private async Task<(Order? Order, string? Error)> LoadForEditAsync(int orderId, string user, bool isAdmin)
        {
            var (order, error) = await LoadAsync(orderId, user, isAdmin, OrderStatus.InProcess);
            if (order is null && error != NotFoundMessage)
                error += " Devolvela a edición para modificarla.";
            return (order, error);
        }

        private async Task<Order> NewOrderAsync(int clientId, string user, DateTime now)
        {
            var next = await _orderRepository.NextOrderNumberAsync();
            var order = new Order
            {
                OrderNumber = $"SAM{next:D7}",
                ClientID = clientId,
                OrderStatus = OrderStatus.InProcess,
                CreatedBy = user,
                CreatedOn = now
            };
            _orderRepository.AddOrder(order);
            return order;
        }

        // Productos de todas las líneas en una consulta. Falla con la lista de los que ya no se pueden vender.
        private async Task<(Dictionary<int, Models.Product.Product>? Products, string? Error)> LoadSellableProductsAsync(Order order)
        {
            var products = await _productRepository.GetByIdsAsync(order.OrderLines.Select(l => l.ProductID));
            var problems = new List<string>();
            foreach (var line in order.OrderLines)
            {
                if (!products.TryGetValue(line.ProductID, out var product) || product.StatusID != 1)
                    problems.Add($"{line.ProductCode}: el producto ya no está activo");
                else if (product.Price is null or <= 0)
                    problems.Add($"{line.ProductCode}: el producto ya no tiene precio");
            }
            return problems.Count > 0
                ? (null, $"Hay productos que no se pueden vender: {string.Join("; ", problems)}.")
                : (products, null);
        }

        // Aplica un movimiento de stock por cada (línea, cantidad), sin guardar. Las líneas de inventario se cargan en
        // una sola consulta. Devuelve el primer error; quien llama debe hacer DiscardChanges si hay error.
        private async Task<string?> ApplyToLinesAsync(Order order, IEnumerable<(OrderLine Line, int Quantity)> items,
            TransactionType type, string? reason, string user, DateTime now)
        {
            var list = items.Where(i => i.Quantity > 0).ToList();
            if (list.Count == 0)
                return null;

            var inventoryLines = await _inventoryLineRepository.GetForUpdateByProductsAsync(list.Select(i => i.Line.ProductID));
            foreach (var (line, quantity) in list)
            {
                if (!inventoryLines.TryGetValue(line.ProductID, out var inventoryLine))
                    return $"{line.ProductCode}: no está en el inventario.";

                var move = _inventoryTransactionServices.ApplyMovement(inventoryLine, type, quantity,
                    ReferenceType.Orden, order.OrderNumber, reason, user, now);
                if (!move.Success)
                    return $"{line.ProductCode}: {move.Error}";
            }
            return null;
        }

        private static void Touch(Order order, string user, DateTime now)
        {
            order.ModifiedBy = user;
            order.ModifiedOn = now;
        }

        // 1.13 o 1 según la orden lleve IVA o no (ver OrderPricing.EffectiveIvaRate).
        private decimal IvaRate(Order order) => OrderPricing.EffectiveIvaRate(order.ApplyIva, _pricing.Value.IvaRate);

        // Recalcula el precio con IVA y el total de una línea a partir de su UnitPrice, cantidad y descuento.
        private static void RecalculateLine(OrderLine line, decimal ivaRate)
        {
            line.UnitPriceIVA = OrderPricing.PriceWithIva(line.UnitPrice, ivaRate);
            var subtotal = OrderPricing.LineSubtotal(line.UnitPrice, line.Quantity, line.Discount);
            line.LineTotalPrice = OrderPricing.LineTotalPrice(subtotal, OrderPricing.LineIva(subtotal, ivaRate));
        }

        // excluded: una línea marcada para borrar sigue en la colección hasta el SaveChanges, así que se excluye a mano.
        private static void RecalculateTotal(Order order, OrderLine? excluded = null) =>
            order.OrderTotalAmount = order.OrderLines.Where(l => l != excluded).Sum(l => l.LineTotalPrice);

        // Dos clics a la vez pueden chocar: misma orden InProcess del cliente o misma línea (índices únicos, SQL 2601/2627),
        // o la misma fila modificada por los dos (RowVersion). Se descarta todo y se repite UNA vez: el segundo intento
        // vuelve a leer de la base y valida de nuevo. Cualquier otro error se registra y se relanza.
        private async Task<OperationResult> WithRetryAsync(Func<Task<OperationResult>> operation, string recordId, string action,
            typeEvent failEvent = typeEvent.Error)
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        return await operation();
                    }
                    catch (DbUpdateConcurrencyException) when (attempt == 1)
                    {
                        _orderRepository.DiscardChanges();
                    }
                    catch (DbUpdateException ex) when (attempt == 1 && IsUniqueViolation(ex))
                    {
                        _orderRepository.DiscardChanges();
                    }
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                _orderRepository.DiscardChanges();
                return OperationResult.Fail("La orden cambió mientras se procesaba. Intentá de nuevo.");
            }
            catch (Exception ex)
            {
                // DiscardChanges antes del log: LogAsync guarda con el mismo contexto y se llevaría los cambios a medias.
                _orderRepository.DiscardChanges();
                await _eventlogServices.LogAsync(failEvent, ModuleName, OrdersTable, recordId,
                    $"Excepción no controlada al {action}.", stackTrace: ex.ToString());
                throw;
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is SqlException { Number: 2601 or 2627 };

        // Completar rechazado por una regla de negocio: queda en la bitácora para que el admin vea por qué.
        // No hay cambios pendientes que proteger (o ya se descartaron), así que el log no guarda nada de más.
        private async Task<OperationResult> CompleteFailAsync(Order order, string error)
        {
            await _eventlogServices.LogAsync(typeEvent.CompleteFail, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"No se pudo completar la orden {order.OrderNumber}: {error}");
            return OperationResult.Fail(error);
        }

        private Task LogStatusChangeAsync(typeEvent type, Order order, OrderStatus previous, string action) =>
            _eventlogServices.LogAsync(type, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"Orden {order.OrderNumber} {action}.",
                preData: new { OrderStatus = previous },
                postData: new { order.OrderStatus, order.OrderTotalAmount, order.ApplyIva, Lines = order.OrderLines.Count });

        // Solo se registra la creación de la orden: cada línea agregada ya deja su ProductoReservado en el kardex.
        // Se pasa un objeto anónimo y no la entidad: Order → OrderLines → Order es un ciclo que el serializador no soporta.
        private Task LogOrderCreatedAsync(Order order) =>
            _eventlogServices.LogAsync(typeEvent.Add, ModuleName, OrdersTable, order.OrderID.ToString(),
                $"Orden {order.OrderNumber} creada.",
                postData: new
                {
                    order.OrderID,
                    order.OrderNumber,
                    order.ClientID,
                    order.OrderStatus,
                    order.ApplyIva,
                    order.OrderTotalAmount,
                    order.CreatedBy,
                    order.CreatedOn
                });
    }
}
