using CatalogStore.BackendAPI.Models.EmailOutbox;
using CatalogStore.BackendAPI.Repository.Email;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Email
{
    public class EmailServices : IEmailServices
    {
        private readonly IEmailOutboxRepository _repository;
        private readonly IOptions<EmailOptions> _options;
        private readonly TimeProvider _time;

        public EmailServices(IEmailOutboxRepository repository, IOptions<EmailOptions> options, TimeProvider time)
        {
            _repository = repository;
            _options = options;
            _time = time;
        }

        public bool Enabled => _options.Value.Enabled;

        public void Queue(EmailMessage message, string createdBy) => _repository.Add(Build(message, createdBy));

        public async Task QueueAndSaveAsync(EmailMessage message, string createdBy)
        {
            var email = Build(message, createdBy);
            _repository.Add(email);
            try
            {
                await _repository.SaveChangesAsync();
            }
            catch
            {
                // Que no quede en el contexto: el próximo SaveChanges (por ejemplo, el de la bitácora) fallaría igual.
                _repository.Detach(email);
                throw;
            }
        }

        public string UiLink(string path, IDictionary<string, string> query)
        {
            var baseUrl = _options.Value.UiBaseUrl.TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("Email:UiBaseUrl no está configurado.");
            var qs = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            return $"{baseUrl}/{path.TrimStart('/')}" + (qs.Length > 0 ? "?" + qs : string.Empty);
        }

        public Task<bool> WasQueuedRecentlyAsync(string kind, string relatedTable, string relatedId, TimeSpan window) =>
            _repository.AnyQueuedSinceAsync(kind, relatedTable, relatedId, _time.GetUtcNow().UtcDateTime - window);

        private EmailOutbox Build(EmailMessage message, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(message.ToAddress))
                throw new ArgumentException("El correo no tiene destinatario.", nameof(message));

            var now = _time.GetUtcNow().UtcDateTime;
            return new EmailOutbox
            {
                ToAddress = message.ToAddress.Trim(),
                ToName = string.IsNullOrWhiteSpace(message.ToName) ? null : message.ToName.Trim(),
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                Kind = message.Kind,
                RelatedTable = message.RelatedTable,
                RelatedID = message.RelatedID,
                IsSensitive = message.IsSensitive,
                Status = EmailStatus.Pending,
                NextAttemptOn = now,
                CreatedBy = createdBy,
                CreatedOn = now
            };
        }
    }
}
