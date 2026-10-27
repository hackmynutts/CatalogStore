using CatalogStore.BackendAPI.Models.Invoice;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CatalogStore.BackendAPI.Services.Invoicing
{
    // Resultado de UN envío al proveedor.
    //  NotSent = la solicitud seguro no salió (conexión rechazada, nombre no resuelto): es seguro volver a intentar.
    //  Cualquier otra falla de red (tiempo agotado, conexión cortada) es incierta: el proveedor pudo haber emitido.
    public record InvoiceTransportResult(int? HttpStatus, string Body, bool NotSent, string? NetworkError)
    {
        public bool Delivered => HttpStatus is not null;
    }

    public interface IInvoiceTransport
    {
        // Un POST, sin reintentos (el manual: no hay idempotencia, reenviar puede emitir dos veces).
        Task<InvoiceTransportResult> PostAsync(string json, CancellationToken ct = default);
    }

    public class HttpInvoiceTransport : IInvoiceTransport
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<InvoicingOptions> _options;
        public HttpInvoiceTransport(IHttpClientFactory httpClientFactory, IOptionsMonitor<InvoicingOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task<InvoiceTransportResult> PostAsync(string json, CancellationToken ct = default)
        {
            var options = _options.CurrentValue;
            var client = _httpClientFactory.CreateClient("Invoicing");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 5)));
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint) { Content = content };
                request.Headers.Accept.ParseAdd("application/json");
                using var response = await client.SendAsync(request, timeout.Token);
                // Texto crudo: el manual avisa que puede haber errores de PHP o respuestas concatenadas que no son JSON.
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                return new InvoiceTransportResult((int)response.StatusCode, body, false, null);
            }
            catch (HttpRequestException ex) when (ex.InnerException is SocketException
                { SocketErrorCode: SocketError.ConnectionRefused or SocketError.HostNotFound or SocketError.NetworkUnreachable or SocketError.HostUnreachable })
            {
                return new InvoiceTransportResult(null, string.Empty, true, ex.InnerException!.Message);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException)
            {
                return new InvoiceTransportResult(null, string.Empty, false, ex.Message);
            }
        }
    }

    // Lo que se entiende de la respuesta de emisión.
    public record EmissionReply(InvoiceStatus Status, string? Clave, string? XmlBase64, string Message);

    // Lo que se entiende de la respuesta a una consulta de estado.
    public record StatusReply(bool Understood, string? HaciendaStatus, string? Message, string? PdfUrl, string? PdfUrlTicket);

    public static class InvoiceResponse
    {
        // Reglas (manual, secciones 3 y 12):
        //  - con clave de 50 dígitos el comprobante EXISTE → Enviada (y por conciliar si además trae estado ERROR: falló
        //    una etapa posterior y el XML ya pudo haber salido);
        //  - sin clave, la solicitud solo se da por NO emitida si el proveedor lo dice claramente (JSON con estado ERROR);
        //  - cualquier otra cosa (no JSON, sin clave, HTTP raro) es incierta: nunca se reenvía sola.
        public static EmissionReply ParseEmission(InvoiceTransportResult transport)
        {
            if (!transport.Delivered)
                return transport.NotSent
                    ? new EmissionReply(InvoiceStatus.Failed, null, null, $"No se pudo conectar con el proveedor: {transport.NetworkError}")
                    : new EmissionReply(InvoiceStatus.Uncertain, null, null,
                        $"Sin respuesta del proveedor ({transport.NetworkError}). No se sabe si el comprobante se emitió: conciliá antes de volver a intentar.");

            if (!TryRoot(transport.Body, out var root))
                return new EmissionReply(InvoiceStatus.Uncertain, null, null,
                    $"La respuesta del proveedor no es un JSON entendible (HTTP {transport.HttpStatus}). No se sabe si el comprobante se emitió.");

            var clave = Text(root, "clave");
            var state = Text(root, "estado");
            var message = Text(root, "mensaje") ?? Text(root, "message") ?? Text(root, "DetalleMensaje") ?? string.Empty;
            var hasKey = clave is { Length: 50 } && clave.All(char.IsDigit);
            var isError = string.Equals(state, "ERROR", StringComparison.OrdinalIgnoreCase);
            var xml = Text(root, "XMLbase64");

            if (hasKey && !isError)
            {
                var signature = Text(root, "firmaok");
                var note = signature is not null && !signature.Contains("valida", StringComparison.OrdinalIgnoreCase) ? $"Firma: {signature}" : message;
                return new EmissionReply(InvoiceStatus.Sent, clave, xml, note);
            }
            if (hasKey && isError)
                return new EmissionReply(InvoiceStatus.Uncertain, clave, xml, $"El proveedor devolvió clave pero también un error: {message}");
            if (isError && transport.HttpStatus is >= 200 and < 300)
                return new EmissionReply(InvoiceStatus.Failed, null, null, string.IsNullOrWhiteSpace(message) ? "El proveedor rechazó la solicitud." : message);

            return new EmissionReply(InvoiceStatus.Uncertain, null, null,
                $"El proveedor respondió sin clave (HTTP {transport.HttpStatus}). No se sabe si el comprobante se emitió. {message}".Trim());
        }

        public static StatusReply ParseStatus(InvoiceTransportResult transport)
        {
            if (!transport.Delivered || !TryRoot(transport.Body, out var root))
                return new StatusReply(false, null, "No se pudo leer la respuesta de la consulta.", null, null);
            var status = Text(root, "codigorespuesta");
            if (status is null)
                return new StatusReply(false, null, Text(root, "mensaje") ?? Text(root, "message") ?? "La consulta no devolvió estado.", null, null);
            return new StatusReply(true, status, Text(root, "DetalleMensaje"), Text(root, "EnlacePDF"), Text(root, "EnlacePDFPV"));
        }

        // aceptado / rechazado son estados finales; recibido y procesando siguen pendientes.
        public static InvoiceStatus? FinalStatus(string? haciendaStatus)
        {
            var s = haciendaStatus?.Trim().ToLowerInvariant() ?? string.Empty;
            if (s.StartsWith("acept")) return InvoiceStatus.Accepted;
            if (s.StartsWith("rechaz")) return InvoiceStatus.Rejected;
            return null;
        }

        private static bool TryRoot(string body, out JsonElement root)
        {
            root = default;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
                root = doc.RootElement.Clone();
                return true;
            }
            catch (JsonException) { return false; }
        }

        // Los nombres exactos del manual ("XMLbase64", "EnlacePDF"); se aceptan sin distinguir mayúsculas por si el proveedor las cambia.
        private static string? Text(JsonElement root, string name)
        {
            foreach (var p in root.EnumerateObject())
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String)
                    return p.Value.GetString();
            return null;
        }
    }
}
