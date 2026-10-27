using CatalogStore.BackendAPI.Models.EmailOutbox;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net;
using System.Text.RegularExpressions;

namespace CatalogStore.BackendAPI.Services.Email
{
    // Envía por SMTP. Separado del worker para poder probar la cola con un transporte falso.
    public interface IEmailTransport
    {
        // Envía el lote con UNA conexión. Devuelve, por EmailOutboxID, null si salió o el mensaje de error.
        Task<Dictionary<int, string?>> SendAsync(IReadOnlyList<EmailOutbox> emails, CancellationToken ct);
    }

    public class SmtpEmailTransport : IEmailTransport
    {
        private readonly IOptionsMonitor<EmailOptions> _options;
        public SmtpEmailTransport(IOptionsMonitor<EmailOptions> options)
        {
            _options = options;
        }

        public async Task<Dictionary<int, string?>> SendAsync(IReadOnlyList<EmailOutbox> emails, CancellationToken ct)
        {
            var o = _options.CurrentValue;
            var results = new Dictionary<int, string?>();
            using var client = new SmtpClient { Timeout = 30_000 };
            try
            {
                var security = o.UseStartTls ? SecureSocketOptions.StartTls
                    : o.Port == 465 ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.Auto;
                await client.ConnectAsync(o.Host, o.Port, security, ct);
                if (!string.IsNullOrEmpty(o.UserName))
                    await client.AuthenticateAsync(o.UserName, o.Password, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Sin conexión o credenciales malas: todo el lote falla con el mismo motivo y se reintenta después.
                foreach (var e in emails)
                    results[e.EmailOutboxID] = $"Conexión SMTP: {ex.Message}";
                return results;
            }

            foreach (var email in emails)
            {
                try
                {
                    await client.SendAsync(BuildMessage(email, o), ct);
                    results[email.EmailOutboxID] = null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    results[email.EmailOutboxID] = ex.Message;
                }
            }

            try { await client.DisconnectAsync(true, ct); } catch { /* el envío ya terminó */ }
            return results;
        }

        private static MimeMessage BuildMessage(EmailOutbox email, EmailOptions o)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
            message.To.Add(new MailboxAddress(email.ToName ?? string.Empty, email.ToAddress));
            message.Subject = email.Subject;
            // Versión en texto plano además del HTML: algunos clientes la usan y mejora la entrega (menos spam).
            message.Body = new BodyBuilder { HtmlBody = email.HtmlBody, TextBody = ToPlainText(email.HtmlBody) }.ToMessageBody();
            return message;
        }

        private static string ToPlainText(string html)
        {
            var text = Regex.Replace(html, @"<(br|/p|/div|/tr|/h\d)\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<a\s[^>]*href=""([^""]+)""[^>]*>(.*?)</a>", "$2 ($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            text = Regex.Replace(text, "<[^>]+>", string.Empty);
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n").Trim();
        }
    }
}
