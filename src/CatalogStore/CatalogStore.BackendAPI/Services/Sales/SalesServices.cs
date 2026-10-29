using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Sales;
using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Models.InventoryTransaction;
using CatalogStore.BackendAPI.Models.Invoice;
using CatalogStore.BackendAPI.Repository.Invoices;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Models.SalesLine;
using CatalogStore.BackendAPI.Models.SalesPayment;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Repository.Orders;
using CatalogStore.BackendAPI.Repository.Sales;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.InventoryTransactions;
using CatalogStore.BackendAPI.Services.Orders;
using Humanizer;
using Microsoft.EntityFrameworkCore;

namespace CatalogStore.BackendAPI.Services.Sales
{
    public class SalesServices : ISalesServices
    {
        private const string ModuleName = "Ventas/Ventas";
        private const string SalesTable = "SalesHeader";
        public const string NotFoundMessage = "Venta no encontrada.";
        private readonly ISalesRepository _salesRepository;
        private readonly IClientServices _clientServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;
        private readonly IOrderRepository _orderRepository;
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IInventoryTransactionServices _inventoryTransactionServices;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly Services.Email.IEmailServices _emailServices;
        private readonly Microsoft.AspNetCore.Identity.UserManager<Data.ApplicationUser> _userManager;
        public SalesServices( ISalesRepository salesRepository, IClientServices clientServices, IEventlogServices eventlogServices, TimeProvider time,
                            IOrderRepository orderRepository, IInventoryLineRepository inventoryLineRepository, IInventoryTransactionServices inventoryTransactionServices,
                            IInvoiceRepository invoiceRepository, Services.Email.IEmailServices emailServices,
                            Microsoft.AspNetCore.Identity.UserManager<Data.ApplicationUser> userManager)
        {
            _invoiceRepository = invoiceRepository;
            _emailServices = emailServices;
            _userManager = userManager;
            _salesRepository = salesRepository;
            _clientServices= clientServices;
            _eventlogServices = eventlogServices;
            _time = time;
            _orderRepository = orderRepository;
            _inventoryLineRepository = inventoryLineRepository;
            _inventoryTransactionServices = inventoryTransactionServices;
        }
        public async Task<SalesHeader> CreateFromOrderAsync(Order order, string createdBy, DateTime now)
        {
            Models.Client.Client cliente = await _clientServices.GetClientAsync(order.ClientID);
            if (cliente is null)
                throw new InvalidOperationException($"El cliente debe de existir para generarle una venta a la orden {order.OrderNumber}");

            var lineasVTA = new List<SalesLine>();
            decimal discountTotal = 0m;
            foreach (var line in order.OrderLines.OrderBy(l => l.OrderLineID))
            {
                var subtotalLine = OrderPricing.LineSubtotal(line.UnitPrice, line.Quantity, line.Discount);
                lineasVTA.Add(new SalesLine
                {
                    ProductID = line.ProductID,
                    ProductCode = line.ProductCode,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitPriceIVA = line.UnitPriceIVA,
                    Discount = line.Discount,
                    LineSubtotal = subtotalLine,
                    LineTotalPrice = line.LineTotalPrice,
                    LineIva = (line.LineTotalPrice- subtotalLine),
                    CreatedBy = createdBy,
                    CreatedOn = now
                });
                discountTotal += ((line.UnitPrice*line.Quantity)-subtotalLine);
            }
            var subtotal = lineasVTA.Sum(l => l.LineSubtotal);
            var ivaTotal = lineasVTA.Sum(l => l.LineIva);
            var plazo = cliente.Credito ?? credit.D0;
            var dias = PaymentTerms.Days(plazo);

            int next = await _salesRepository.NextSalesNumberAsync();
            
            var sale = new SalesHeader
            {
                OrderID = order.OrderID,
                SaleNumber = $"VTA{next:D7}",
                ClientID = order.ClientID,
                ClientIdentificationType = cliente.IdentificationType,
                ClientIdentification = cliente.Identification,
                ClientName = cliente.ClientName,
                ClientEmail = cliente.ClientEmail,
                ClientPhone = cliente.ClientPhone,
                ClientAddress = cliente.ClientAddress,
                ClientCC = cliente.CC,
                SaleDate = now,
                Seller = order.CreatedBy,
                ApplyIva = order.ApplyIva,
                Notes = order.Notes,
                PaymentTerm = plazo,
                DueDate = DateOnly.FromDateTime(_time.GetLocalNow().DateTime.AddDays(dias)),
                CreatedBy = createdBy,
                CreatedOn = now,
                TotalAmount = order.OrderTotalAmount,
                Subtotal = subtotal,
                IvaTotal = ivaTotal,
                DiscountTotal = discountTotal,
                Lines = lineasVTA
            };

            if ((sale.Subtotal + sale.IvaTotal) != sale.TotalAmount)
                throw new InvalidOperationException($"Los montos no coinciden entre el subtotal de la venta {sale.Subtotal} junto con el iva total {sale.IvaTotal} " +
                                                    $"es diferente al monto de la venta {sale.TotalAmount}, con una diferencia de {sale.TotalAmount- (sale.Subtotal + sale.IvaTotal)}");

            _salesRepository.AddSale(sale);
            return sale;
        }

        // ───────────────────────── Lectura ─────────────────────────

        public async Task<SaleDTO?> GetAsync(int salesHeaderId, string user, bool isAdmin)
        {
            var sale = await _salesRepository.GetByIdAsync(salesHeaderId);
            return sale is null || !CanAccess(sale, user, isAdmin) ? null : Map(sale);
        }

        public async Task<SaleDTO?> GetByOrderAsync(int orderId, string user, bool isAdmin)
        {
            var sale = await _salesRepository.GetByOrderIdAsync(orderId);
            return sale is null || !CanAccess(sale, user, isAdmin) ? null : Map(sale);
        }

        // El vendedor recibe siempre solo sus ventas, aunque no lo pida.
        public async Task<List<SaleListItemDTO>> GetListAsync(PaymentStatus? status, int? clientId, bool overdueOnly, string user, bool isAdmin)
        {
            var today = TodayLocal();
            var rows = await _salesRepository.GetListAsync(status, clientId, isAdmin ? null : user, overdueOnly ? today : null);
            return rows.Select(r =>
            {
                var (isOverdue, daysOverdue) = Overdue(r.Sale, today);
                return new SaleListItemDTO
                {
                    SalesHeaderID = r.Sale.SalesHeaderID,
                    SaleNumber = r.Sale.SaleNumber,
                    OrderID = r.Sale.OrderID,
                    ClientID = r.Sale.ClientID,
                    ClientName = r.Sale.ClientName,
                    ClientCC = r.Sale.ClientCC,
                    Seller = r.Sale.Seller,
                    SaleDate = r.Sale.SaleDate,
                    ApplyIva = r.Sale.ApplyIva,
                    TotalAmount = r.Sale.TotalAmount,
                    AmountPaid = r.Sale.AmountPaid,
                    Balance = r.Sale.Balance,
                    PaymentTermName = r.Sale.PaymentTerm.Humanize(),
                    DueDate = r.Sale.DueDate,
                    PaymentStatus = r.Sale.PaymentStatus,
                    PaymentStatusName = r.Sale.PaymentStatus.Humanize(),
                    IsOverdue = isOverdue,
                    DaysOverdue = daysOverdue,
                    LineCount = r.LineCount
                };
            }).ToList();
        }

        // ───────────────────────── Abonos ─────────────────────────

        // Registra un abono. Lo pagado se RECALCULA desde los abonos vigentes (no se suma al valor anterior):
        // así AmountPaid nunca se desincroniza. Dos abonos a la vez chocan con el RowVersion de la venta.
        public async Task<OperationResult> RegisterPaymentAsync(int salesHeaderId, AddSalePaymentDTO dto, string user, bool isAdmin)
        {
            try
            {
                var now = _time.GetUtcNow().UtcDateTime;
                var sale = await _salesRepository.GetForUpdateAsync(salesHeaderId);
                if (sale is null || !CanAccess(sale, user, isAdmin))
                    return OperationResult.Fail(NotFoundMessage);
                if (sale.PaymentStatus == PaymentStatus.Voided)
                    return OperationResult.Fail($"La venta {sale.SaleNumber} está anulada y no acepta abonos.");
                if (dto.PaymentMethod is null || !Enum.IsDefined(dto.PaymentMethod.Value))
                    return OperationResult.Fail("Seleccione un método de pago válido.");
                if (dto.Amount <= 0)
                    return OperationResult.Fail("El monto debe ser mayor a cero.");

                var balance = sale.TotalAmount - PaidAmount(sale);
                if (balance <= 0)
                    return OperationResult.Fail($"La venta {sale.SaleNumber} ya está pagada.");
                if (dto.Amount > balance)
                    return OperationResult.Fail($"El abono (₡{dto.Amount:N2}) supera el saldo pendiente (₡{balance:N2}).");

                var (paymentDate, dateError) = ResolvePaymentDate(dto.PaymentDate, sale, now);
                if (dateError is not null)
                    return OperationResult.Fail(dateError);

                var payment = new SalesPayment
                {
                    Amount = dto.Amount,
                    PaymentMethod = dto.PaymentMethod.Value,
                    Reference = string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(),
                    PaymentDate = paymentDate,
                    Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                    CreatedBy = user,
                    CreatedOn = now
                };
                sale.Payments.Add(payment);
                RecalculatePayments(sale, user, now);
                await _salesRepository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Add, ModuleName, "SalesPayment", payment.SalesPaymentID.ToString(),
                    $"Abono de ₡{payment.Amount:N2} ({payment.PaymentMethod.Humanize()}) registrado en {sale.SaleNumber}. Saldo: ₡{sale.TotalAmount - sale.AmountPaid:N2}.",
                    postData: new { payment.SalesPaymentID, sale.SalesHeaderID, sale.SaleNumber, payment.Amount, payment.PaymentMethod, payment.Reference, payment.PaymentDate, sale.AmountPaid, sale.PaymentStatus });
                return OperationResult.Ok(sale.SalesHeaderID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _salesRepository.DiscardChanges();
                return OperationResult.Fail("La venta cambió mientras se procesaba (otro abono al mismo tiempo). Revisá el saldo e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                // DiscardChanges antes del log: LogAsync guarda con el mismo contexto y se llevaría los cambios a medias.
                _salesRepository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.Error, ModuleName, SalesTable, salesHeaderId.ToString(),
                    "Excepción no controlada al registrar un abono.", stackTrace: ex.ToString());
                throw;
            }
        }

        // Anula un abono (solo admin). No se borra: queda marcado con quién, cuándo y por qué, y deja de contar.
        public async Task<OperationResult> VoidPaymentAsync(int salesHeaderId, int salesPaymentId, VoidSalePaymentDTO dto, string user, bool isAdmin)
        {
            if (!isAdmin)
                return OperationResult.Fail("Solo un administrador puede anular abonos.");
            try
            {
                var now = _time.GetUtcNow().UtcDateTime;
                var sale = await _salesRepository.GetForUpdateAsync(salesHeaderId);
                if (sale is null)
                    return OperationResult.Fail(NotFoundMessage);

                var payment = sale.Payments.FirstOrDefault(p => p.SalesPaymentID == salesPaymentId);
                if (payment is null)
                    return OperationResult.Fail("El abono no pertenece a esta venta.");
                if (payment.IsVoided)
                    return OperationResult.Fail("El abono ya estaba anulado.");
                var reason = dto.Reason?.Trim();
                if (string.IsNullOrEmpty(reason))
                    return OperationResult.Fail("Indique el motivo de la anulación.");

                // Los cuatro campos juntos: el CHECK CK_SalesPayment_Void_Consistent rechaza un anulado incompleto.
                payment.IsVoided = true;
                payment.VoidedBy = user;
                payment.VoidedOn = now;
                payment.VoidedReason = reason;
                RecalculatePayments(sale, user, now);
                await _salesRepository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Cancel, ModuleName, "SalesPayment", payment.SalesPaymentID.ToString(),
                    $"Abono de ₡{payment.Amount:N2} anulado en {sale.SaleNumber}. Motivo: {reason}",
                    preData: new { IsVoided = false },
                    postData: new { payment.SalesPaymentID, sale.SaleNumber, payment.Amount, payment.VoidedReason, sale.AmountPaid, sale.PaymentStatus });
                await NotifySellerPaymentVoidedAsync(sale, payment, reason, user);
                return OperationResult.Ok(sale.SalesHeaderID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _salesRepository.DiscardChanges();
                return OperationResult.Fail("La venta cambió mientras se procesaba. Revisá el saldo e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                _salesRepository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.CancelFail, ModuleName, SalesTable, salesHeaderId.ToString(),
                    "Excepción no controlada al anular un abono.", stackTrace: ex.ToString());
                throw;
            }
        }

        // Avisa por correo al vendedor de la venta que le anularon un abono (cambia su saldo y lo que se le cobra al cliente).
        // Va DESPUÉS de guardar la anulación y nunca la deshace: si el correo falla, solo queda el rastro en la bitácora.
        // Se omite si el vendedor es quien anuló, si el correo está deshabilitado o si el vendedor apagó sus notificaciones.
        private async Task NotifySellerPaymentVoidedAsync(SalesHeader sale, SalesPayment payment, string reason, string voidedBy)
        {
            try
            {
                if (!_emailServices.Enabled || string.Equals(sale.Seller, voidedBy, StringComparison.OrdinalIgnoreCase)) return;
                // SalesHeader.Seller es el correo del vendedor (claim unique_name del JWT).
                var seller = await _userManager.FindByEmailAsync(sale.Seller);
                if (seller is null || !seller.SendNotifications || string.IsNullOrWhiteSpace(seller.Email)) return;

                var paidOn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(payment.PaymentDate, DateTimeKind.Utc), _time.LocalTimeZone).Date;
                await _emailServices.QueueAndSaveAsync(new Services.Email.EmailMessage(
                    seller.Email, seller.FullName,
                    $"Se anuló un abono de la venta {sale.SaleNumber}",
                    Services.Email.EmailTemplates.PaymentVoided(
                        string.IsNullOrWhiteSpace(seller.FullName) ? seller.UserName ?? string.Empty : seller.FullName.Trim(),
                        sale.SaleNumber, sale.ClientName, payment.Amount, payment.PaymentMethod.Humanize(), paidOn, reason, voidedBy,
                        Math.Max(sale.TotalAmount - sale.AmountPaid, 0m),
                        _emailServices.UiLink($"Sales/Details/{sale.SalesHeaderID}", new Dictionary<string, string>())),
                    Services.Email.EmailKinds.PaymentVoided, "SalesHeader", sale.SalesHeaderID.ToString()), voidedBy);
            }
            catch (Exception ex)
            {
                await _eventlogServices.LogAsync(typeEvent.Error, ModuleName, "SalesPayment", payment.SalesPaymentID.ToString(),
                    "No se pudo avisar al vendedor de la anulación del abono.", stackTrace: ex.ToString());
            }
        }

        // ───────────────────────── Anulación ─────────────────────────

        // Anula la venta completa (solo admin). Exige que no queden abonos vigentes: la plata recibida se anula
        // abono por abono, cada uno con su motivo. Devuelve al stock lo vendido (movimiento Devolucion con
        // referencia a la venta), marca la venta Anulada y su orden Retornada, y guarda todo junto.
        public async Task<OperationResult> VoidSaleAsync(int salesHeaderId, VoidSaleDTO dto, string user, bool isAdmin)
        {
            if (!isAdmin)
                return OperationResult.Fail("Solo un administrador puede anular ventas.");
            var reason = dto.Reason?.Trim();
            if (string.IsNullOrEmpty(reason))
                return OperationResult.Fail("Indique el motivo de la anulación.");
            if (reason.Length > 250)
                return OperationResult.Fail("El motivo no puede exceder los 250 caracteres.");

            try
            {
                var now = _time.GetUtcNow().UtcDateTime;
                var sale = await _salesRepository.GetForVoidAsync(salesHeaderId);
                if (sale is null)
                    return OperationResult.Fail(NotFoundMessage);
                if (sale.PaymentStatus == PaymentStatus.Voided)
                    return OperationResult.Fail($"La venta {sale.SaleNumber} ya está anulada.");
                var paid = PaidAmount(sale);
                if (paid > 0)
                    return OperationResult.Fail($"La venta {sale.SaleNumber} tiene ₡{paid:N2} en abonos vigentes. Anúlelos primero.");
                var invoiceBlock = await InvoiceBlockAsync(sale);
                if (invoiceBlock is not null)
                    return OperationResult.Fail(invoiceBlock);

                var order = await _orderRepository.GetForUpdateAsync(sale.OrderID)
                    ?? throw new InvalidOperationException($"La venta {sale.SaleNumber} apunta a la orden {sale.OrderID}, que no existe.");
                if (order.OrderStatus != OrderStatus.Completed)
                    return OperationResult.Fail($"La orden {order.OrderNumber} está {order.OrderStatus.Humanize().ToLower()} y su venta no se puede anular.");

                // Las cantidades salen de la venta: la devolución es el espejo del documento que se anula.
                var inventoryLines = await _inventoryLineRepository.GetForUpdateByProductsAsync(sale.Lines.Select(l => l.ProductID));
                foreach (var line in sale.Lines.OrderBy(l => l.SalesLineID))
                {
                    string? error = null;
                    if (!inventoryLines.TryGetValue(line.ProductID, out var inventoryLine))
                        error = $"{line.ProductCode}: no está en el inventario.";
                    else
                    {
                        var move = _inventoryTransactionServices.ApplyMovement(inventoryLine, TransactionType.Devolucion, line.Quantity,
                            ReferenceType.Venta, sale.SaleNumber, $"Anulación: {reason}", user, now);
                        if (!move.Success)
                            error = $"{line.ProductCode}: {move.Error}";
                    }
                    if (error is not null)
                    {
                        // Las líneas anteriores ya quedaron modificadas en el contexto: se descartan antes de responder.
                        _salesRepository.DiscardChanges();
                        return OperationResult.Fail(error);
                    }
                }

                var previousSale = sale.PaymentStatus;
                var previousOrder = order.OrderStatus;

                // Los cuatro juntos: el CHECK CK_SalesHeader_Void_Consistent rechaza una anulada incompleta.
                // No se llama a RecalculatePayments: volvería el estado a Pendiente.
                sale.PaymentStatus = PaymentStatus.Voided;
                sale.VoidedBy = user;
                sale.VoidedOn = now;
                sale.VoidedReason = reason;
                sale.ModifiedBy = user;
                sale.ModifiedOn = now;

                order.OrderStatus = OrderStatus.Returned;
                order.ModifiedBy = user;
                order.ModifiedOn = now;

                // Mismo contexto: venta, orden, inventario y transacciones en un solo guardado.
                await _salesRepository.SaveChangesAsync();

                await _eventlogServices.LogAsync(typeEvent.Cancel, ModuleName, SalesTable, sale.SalesHeaderID.ToString(),
                    $"Venta {sale.SaleNumber} anulada (orden {order.OrderNumber} retornada). Motivo: {reason}",
                    preData: new { PaymentStatus = previousSale, OrderStatus = previousOrder },
                    postData: new
                    {
                        sale.SalesHeaderID, sale.SaleNumber, order.OrderNumber, sale.PaymentStatus, order.OrderStatus, sale.VoidedReason,
                        Lines = sale.Lines.Select(l => new { l.ProductID, l.ProductCode, l.Quantity })
                    });
                return OperationResult.Ok(sale.SalesHeaderID);
            }
            catch (DbUpdateConcurrencyException)
            {
                _salesRepository.DiscardChanges();
                return OperationResult.Fail("La venta o su orden cambió mientras se procesaba. Revisá el detalle e intentá de nuevo.");
            }
            catch (Exception ex)
            {
                _salesRepository.DiscardChanges();
                await _eventlogServices.LogAsync(typeEvent.CancelFail, ModuleName, SalesTable, salesHeaderId.ToString(),
                    "Excepción no controlada al anular una venta.", stackTrace: ex.ToString());
                throw;
            }
        }

        // ───────────────────────── Helpers ─────────────────────────

        // Admin ve y cobra cualquier venta; el vendedor solo las suyas.
        private static bool CanAccess(SalesHeader sale, string user, bool isAdmin) => isAdmin || sale.Seller == user;

        private DateOnly TodayLocal() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

        private static decimal PaidAmount(SalesHeader sale) => sale.Payments.Where(p => !p.IsVoided).Sum(p => p.Amount);

        // Una venta con factura o tiquete electrónico vigente no se puede anular hasta que su nota de crédito esté
        // aceptada por Hacienda: si no, el stock volvería y la orden se retornaría con un comprobante fiscal todavía válido.
        private async Task<string?> InvoiceBlockAsync(SalesHeader sale)
        {
            var invoices = await _invoiceRepository.GetBySaleAsync(sale.SalesHeaderID);
            var main = invoices.FirstOrDefault(i => i.DocumentType != InvoiceDocType.NotaCredito && InvoiceStates.Active.Contains(i.Status));
            if (main is null) return null;

            var note = invoices.FirstOrDefault(i => i.DocumentType == InvoiceDocType.NotaCredito && InvoiceStates.Active.Contains(i.Status));
            if (note?.Status == InvoiceStatus.Accepted) return null;

            var label = main.DocumentType == InvoiceDocType.Tiquete ? "tiquete" : "factura";
            return note is null
                ? $"La venta {sale.SaleNumber} tiene un {label} electrónico vigente. Emití primero la nota de crédito (Facturación) y esperá a que Hacienda la acepte."
                : $"La nota de crédito de la venta {sale.SaleNumber} todavía no está aceptada por Hacienda. Actualizá su estado y volvé a intentar.";
        }

        // Lo pagado y el estado salen siempre de la lista de abonos vigentes.
        private static void RecalculatePayments(SalesHeader sale, string user, DateTime now)
        {
            sale.AmountPaid = PaidAmount(sale);
            sale.PaymentStatus = sale.AmountPaid <= 0 ? PaymentStatus.Pending
                : sale.AmountPaid >= sale.TotalAmount ? PaymentStatus.Paid
                : PaymentStatus.PartiallyPaid;
            sale.ModifiedBy = user;
            sale.ModifiedOn = now;
        }

        // La fecha llega como día local (Costa Rica). Hoy: la hora exacta del registro. Otro día: mediodía local de
        // ese día pasado a UTC (mediodía para que ninguna conversión de zona lo corra al día anterior o siguiente).
        private (DateTime Date, string? Error) ResolvePaymentDate(DateOnly? requested, SalesHeader sale, DateTime now)
        {
            var today = TodayLocal();
            var date = requested ?? today;
            if (date > today)
                return (default, "La fecha de pago no puede ser futura.");
            var saleLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(sale.SaleDate, DateTimeKind.Utc), _time.LocalTimeZone);
            var saleLocalDate = DateOnly.FromDateTime(saleLocal);
            if (date < saleLocalDate)
                return (default, $"La fecha de pago no puede ser anterior a la venta ({saleLocalDate:dd/MM/yyyy}).");
            if (date == today)
                return (now, null);
            var localNoon = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
            return (TimeZoneInfo.ConvertTimeToUtc(localNoon, _time.LocalTimeZone), null);
        }

        private static (bool IsOverdue, int Days) Overdue(SalesHeader sale, DateOnly today)
        {
            var hasBalance = sale.PaymentStatus is PaymentStatus.Pending or PaymentStatus.PartiallyPaid;
            return hasBalance && sale.DueDate < today ? (true, today.DayNumber - sale.DueDate.DayNumber) : (false, 0);
        }

        private SaleDTO Map(SalesHeader sale)
        {
            var (isOverdue, daysOverdue) = Overdue(sale, TodayLocal());
            return new SaleDTO
            {
                SalesHeaderID = sale.SalesHeaderID,
                SaleNumber = sale.SaleNumber,
                OrderID = sale.OrderID,
                OrderNumber = sale.Order?.OrderNumber ?? string.Empty,
                ClientID = sale.ClientID,
                ClientName = sale.ClientName,
                ClientIdentification = sale.ClientIdentification,
                ClientIdentificationType = sale.ClientIdentificationType,
                ClientIdentificationTypeName = sale.ClientIdentificationType.Humanize(),
                ClientCC = sale.ClientCC,
                ClientEmail = sale.ClientEmail,
                ClientPhone = sale.ClientPhone,
                ClientAddress = sale.ClientAddress,
                Seller = sale.Seller,
                SaleDate = sale.SaleDate,
                ApplyIva = sale.ApplyIva,
                Subtotal = sale.Subtotal,
                DiscountTotal = sale.DiscountTotal,
                IvaTotal = sale.IvaTotal,
                TotalAmount = sale.TotalAmount,
                PaymentTerm = sale.PaymentTerm,
                PaymentTermName = sale.PaymentTerm.Humanize(),
                DueDate = sale.DueDate,
                AmountPaid = sale.AmountPaid,
                Balance = sale.Balance,
                PaymentStatus = sale.PaymentStatus,
                PaymentStatusName = sale.PaymentStatus.Humanize(),
                IsOverdue = isOverdue,
                DaysOverdue = daysOverdue,
                Notes = sale.Notes,
                VoidedBy = sale.VoidedBy,
                VoidedOn = sale.VoidedOn,
                VoidedReason = sale.VoidedReason,
                CreatedBy = sale.CreatedBy,
                CreatedOn = sale.CreatedOn,
                Lines = sale.Lines.OrderBy(l => l.SalesLineID).Select(l => new SaleLineDTO
                {
                    SalesLineID = l.SalesLineID,
                    ProductID = l.ProductID,
                    ProductCode = l.ProductCode,
                    ProductName = l.ProductName,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    UnitPriceIVA = l.UnitPriceIVA,
                    Discount = l.Discount,
                    LineSubtotal = l.LineSubtotal,
                    LineIva = l.LineIva,
                    LineTotalPrice = l.LineTotalPrice
                }).ToList(),
                Payments = sale.Payments.OrderBy(p => p.PaymentDate).ThenBy(p => p.SalesPaymentID).Select(p => new SalePaymentDTO
                {
                    SalesPaymentID = p.SalesPaymentID,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    PaymentMethodName = p.PaymentMethod.Humanize(),
                    Reference = p.Reference,
                    PaymentDate = p.PaymentDate,
                    Notes = p.Notes,
                    IsVoided = p.IsVoided,
                    VoidedBy = p.VoidedBy,
                    VoidedOn = p.VoidedOn,
                    VoidedReason = p.VoidedReason,
                    CreatedBy = p.CreatedBy,
                    CreatedOn = p.CreatedOn
                }).ToList()
            };
        }
    }
}
