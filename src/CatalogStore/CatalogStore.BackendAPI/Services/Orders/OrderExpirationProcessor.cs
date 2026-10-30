using CatalogStore.BackendAPI.DTO.Order;
using CatalogStore.BackendAPI.Repository.Orders;
using CatalogStore.BackendAPI.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Orders
{
    // Una pasada sobre las órdenes en revisión (Pending):
    //  1. Cancela las que llevan más de Orders:PendingExpirationDays sin completarse. Es lo mismo que hace un administrador
    //     (CancelAsync): libera todo lo reservado y deja el motivo en cada movimiento y en la bitácora, con "Sistema" como autor.
    //  2. Le avisa al vendedor de las que ya pasaron Orders:PendingWarningDays y todavía se pueden salvar (un solo aviso por orden).
    // Scoped (usa el DbContext); OrderExpirationWorker la llama cada hora y las pruebas la pueden llamar directo.
    public class OrderExpirationProcessor
    {
        public const string SystemUser = "Sistema";
        // Tope por pasada: si hubiera cientos, las demás se atienden en las pasadas siguientes.
        public const int BatchSize = 50;
        private const string OrdersTable = "Order";

        private readonly IOrderRepository _repository;
        private readonly IOrderServices _orderServices;
        private readonly IOptionsMonitor<OrdersOptions> _options;
        private readonly IEmailServices _emailServices;
        private readonly UserManager<Data.ApplicationUser> _userManager;
        private readonly TimeProvider _time;
        private readonly ILogger<OrderExpirationProcessor> _logger;

        public OrderExpirationProcessor(IOrderRepository repository, IOrderServices orderServices, IOptionsMonitor<OrdersOptions> options,
                                        IEmailServices emailServices, UserManager<Data.ApplicationUser> userManager,
                                        TimeProvider time, ILogger<OrderExpirationProcessor> logger)
        {
            _repository = repository;
            _orderServices = orderServices;
            _options = options;
            _emailServices = emailServices;
            _userManager = userManager;
            _time = time;
            _logger = logger;
        }

        // Devuelve cuántas órdenes canceló.
        public async Task<int> ProcessAsync(CancellationToken ct)
        {
            var days = _options.CurrentValue.PendingExpirationDays;
            // 0 o menos = función apagada: sirve de interruptor de emergencia sin tocar código.
            if (days <= 0)
                return 0;

            // Antes de buscar: lo que estaba en revisión sin fecha (de antes de esta versión) arranca su plazo hoy.
            var stamped = await _repository.StampMissingPendingSinceAsync(_time.GetUtcNow().UtcDateTime);
            if (stamped > 0)
                _logger.LogInformation("Órdenes en revisión sin fecha de entrada: {Count}. Su plazo de {Days} días empieza hoy.", stamped, days);

            var cancelled = await CancelExpiredAsync(days, ct);
            await WarnAsync(days, ct);
            return cancelled;
        }

        private async Task<int> CancelExpiredAsync(int days, CancellationToken ct)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var expired = await _repository.GetExpiredPendingAsync(now.AddDays(-days), BatchSize);
            if (expired.Count == 0)
                return 0;

            var dto = new CancelOrderDTO { Reason = $"Cancelada automáticamente: más de {days} días en revisión sin completarse." };
            var cancelled = 0;
            foreach (var order in expired)
            {
                ct.ThrowIfCancellationRequested();
                // isAdmin: true porque "Sistema" no es el dueño de la orden. Una orden que cambió de estado entre la búsqueda
                // y este punto (ej. el vendedor la completó) responde con error: es inofensivo y se sigue con la siguiente.
                var result = await _orderServices.CancelAsync(order.OrderID, dto, SystemUser, isAdmin: true);
                if (!result.Success)
                {
                    _logger.LogWarning("No se canceló la orden {OrderId} por vencimiento: {Error}", order.OrderID, result.Error);
                    continue;
                }
                cancelled++;
                await NotifyAsync(order, EmailKinds.OrderExpired,
                    $"Se canceló la orden {order.OrderNumber}",
                    seller => EmailTemplates.OrderAutoCancelled(seller, order.OrderNumber, order.ClientName, order.OrderTotalAmount,
                        DaysSince(order.PendingSince, now), Link(order.OrderID)));
            }

            _logger.LogInformation("Órdenes canceladas por pasar {Days} días en revisión: {Count}.", days, cancelled);
            return cancelled;
        }

        // Aviso previo: solo si está configurado y llega antes de la cancelación. Un aviso por orden: se mira la cola de correos.
        private async Task WarnAsync(int expireDays, CancellationToken ct)
        {
            var warnDays = _options.CurrentValue.PendingWarningDays;
            if (warnDays <= 0 || warnDays >= expireDays)
                return;

            var now = _time.GetUtcNow().UtcDateTime;
            var candidates = await _repository.GetPendingToWarnAsync(now.AddDays(-warnDays), now.AddDays(-expireDays), BatchSize);
            foreach (var order in candidates)
            {
                ct.ThrowIfCancellationRequested();
                // La ventana es el plazo completo: una orden devuelta a edición y reenviada más adelante puede volver a avisar.
                if (await _emailServices.WasQueuedRecentlyAsync(EmailKinds.OrderExpiryWarning, OrdersTable, order.OrderID.ToString(), TimeSpan.FromDays(expireDays)))
                    continue;

                var expiresOnLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(order.PendingSince.AddDays(expireDays), DateTimeKind.Utc), _time.LocalTimeZone);
                await NotifyAsync(order, EmailKinds.OrderExpiryWarning,
                    $"La orden {order.OrderNumber} se cancelará pronto",
                    seller => EmailTemplates.OrderExpiryWarning(seller, order.OrderNumber, order.ClientName, order.OrderTotalAmount,
                        DaysSince(order.PendingSince, now), expiresOnLocal, Link(order.OrderID)));
            }
        }

        // El vendedor es quien creó la orden (CreatedBy guarda su correo). Se omite si el correo está apagado o si apagó sus notificaciones.
        // Nunca deshace la cancelación: si el correo falla, queda en el log y se sigue.
        private async Task NotifyAsync(PendingOrderRow order, string kind, string subject, Func<string, string> body)
        {
            try
            {
                if (!_emailServices.Enabled) return;
                var seller = await _userManager.FindByEmailAsync(order.CreatedBy);
                if (seller is null || !seller.SendNotifications || string.IsNullOrWhiteSpace(seller.Email)) return;

                var name = string.IsNullOrWhiteSpace(seller.FullName) ? seller.UserName ?? string.Empty : seller.FullName.Trim();
                await _emailServices.QueueAndSaveAsync(
                    new EmailMessage(seller.Email, seller.FullName, subject, body(name), kind, OrdersTable, order.OrderID.ToString()),
                    SystemUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo avisar al vendedor de la orden {OrderNumber}.", order.OrderNumber);
            }
        }

        private string Link(int orderId) => _emailServices.UiLink($"Order/Details/{orderId}", new Dictionary<string, string>());

        private static int DaysSince(DateTime sinceUtc, DateTime nowUtc) => Math.Max(0, (int)Math.Floor((nowUtc - sinceUtc).TotalDays));
    }

    // Revisa cada hora: el plazo es de días, no hace falta más seguido. Pensado para UNA instancia de la app (como EmailOutboxWorker);
    // con varias, la segunda recibe "la orden ya no está pendiente" y no duplica nada.
    public class OrderExpirationWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<OrderExpirationWorker> _logger;

        public OrderExpirationWorker(IServiceScopeFactory scopes, ILogger<OrderExpirationWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    // Un scope por pasada: el worker es singleton y el DbContext es scoped.
                    using var scope = _scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<OrderExpirationProcessor>();
                    await processor.ProcessAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Un error de la base no debe matar el worker: se registra y se reintenta en la próxima pasada.
                    _logger.LogError(ex, "Error cancelando las órdenes vencidas en revisión.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
