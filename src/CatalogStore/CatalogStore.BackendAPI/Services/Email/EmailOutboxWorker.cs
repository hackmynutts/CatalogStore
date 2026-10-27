using CatalogStore.BackendAPI.Models.EmailOutbox;
using CatalogStore.BackendAPI.Repository.Email;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Email
{
    // Procesa UNA pasada de la cola: toma los pendientes que ya tocan, los envía y registra el resultado.
    // Scoped (usa el DbContext). El worker lo llama cada PollSeconds; las pruebas lo pueden llamar directo.
    public class EmailOutboxProcessor
    {
        public const int BatchSize = 20;
        public const string ScrubbedBody = "[Contenido eliminado: contenía un enlace de acceso.]";

        private readonly IEmailOutboxRepository _repository;
        private readonly IEmailTransport _transport;
        private readonly IOptionsMonitor<EmailOptions> _options;
        private readonly TimeProvider _time;

        public EmailOutboxProcessor(IEmailOutboxRepository repository, IEmailTransport transport, IOptionsMonitor<EmailOptions> options, TimeProvider time)
        {
            _repository = repository;
            _transport = transport;
            _options = options;
            _time = time;
        }

        public async Task<int> ProcessDueAsync(CancellationToken ct)
        {
            var o = _options.CurrentValue;
            if (!o.Enabled)
                return 0;

            var due = await _repository.GetDueAsync(_time.GetUtcNow().UtcDateTime, BatchSize);
            if (due.Count == 0)
                return 0;

            var results = await _transport.SendAsync(due, ct);
            var now = _time.GetUtcNow().UtcDateTime;
            foreach (var email in due)
            {
                email.Attempts++;
                results.TryGetValue(email.EmailOutboxID, out var error);
                if (!results.ContainsKey(email.EmailOutboxID))
                    error = "El transporte no devolvió resultado.";

                if (error is null)
                {
                    email.Status = EmailStatus.Sent;
                    email.SentOn = now;
                    email.LastError = null;
                    Scrub(email);
                }
                else
                {
                    email.LastError = error.Length > 1000 ? error[..1000] : error;
                    if (email.Attempts >= o.MaxAttempts)
                    {
                        email.Status = EmailStatus.Failed;
                        Scrub(email);
                    }
                    else
                        email.NextAttemptOn = now + Backoff(email.Attempts);
                }
            }
            await _repository.SaveChangesAsync();
            return due.Count;
        }

        // 1 min, 5 min, 15 min y después cada hora.
        public static TimeSpan Backoff(int attempts) => attempts switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromHours(1)
        };

        // Un enlace de reset o invitación no debe quedar guardado una vez que ya no hace falta.
        private static void Scrub(EmailOutbox email)
        {
            if (email.IsSensitive)
                email.HtmlBody = ScrubbedBody;
        }
    }

    // Revisa la cola cada PollSeconds. Pensado para UNA instancia de la app (App Service sin escalar horizontalmente):
    // con varias instancias dos workers podrían tomar el mismo correo.
    public class EmailOutboxWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IOptionsMonitor<EmailOptions> _options;
        private readonly ILogger<EmailOutboxWorker> _logger;

        public EmailOutboxWorker(IServiceScopeFactory scopes, IOptionsMonitor<EmailOptions> options, ILogger<EmailOutboxWorker> logger)
        {
            _scopes = scopes;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.CurrentValue.Enabled)
                _logger.LogWarning("Email:Enabled = false. Los correos se encolan en EmailOutbox_TB pero no se envían.");

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.CurrentValue.PollSeconds)));
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>();
                    // Mientras haya lotes llenos sigue, sin esperar al próximo tick.
                    while (await processor.ProcessDueAsync(stoppingToken) == EmailOutboxProcessor.BatchSize) { }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Un error de la base no debe matar el worker: se registra y se reintenta en el próximo tick.
                    _logger.LogError(ex, "Error procesando la cola de correos.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
