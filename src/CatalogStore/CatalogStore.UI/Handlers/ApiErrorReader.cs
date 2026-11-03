using System.Net;
using System.Text.Json;

namespace CatalogStore.UI.Handlers
{
    // Convierte una respuesta de error de la API en un mensaje legible para mostrar al usuario.
    // La API responde { message } en reglas de negocio y ProblemDetails { errors: { campo: [..] } } en validaciones.
    public static class ApiErrorReader
    {
        // Un solo texto (los mensajes unidos por espacio): para las pantallas que lo muestran en una línea.
        public static async Task<string> ReadAsync(HttpResponseMessage response, string fallback) =>
            string.Join(" ", await ReadMessagesAsync(response, fallback));

        // Cada motivo por separado: para mostrar la lista completa de validaciones en un SweetAlert.
        // Nunca devuelve una lista vacía: si no hay motivo claro, devuelve el texto de respaldo.
        public static async Task<List<string>> ReadMessagesAsync(HttpResponseMessage response, string fallback)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return new() { "No tenés permiso para esta acción." };
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(message.GetString()))
                    return new() { message.GetString()! };
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var list = errors.EnumerateObject()
                        .SelectMany(e => e.Value.EnumerateArray().Select(v => Friendly(e.Name, v.GetString())))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct()
                        .ToList();
                    if (list.Count > 0)
                        return list!;
                }
            }
            catch (JsonException) { }
            catch (InvalidOperationException) { }
            // Sin motivo en la respuesta: un 5xx es un fallo del servidor y queda en la bitácora de eventos.
            return (int)response.StatusCode >= 500
                ? new() { $"{fallback} Error inesperado del servidor ({(int)response.StatusCode}); quedó registrado en la bitácora." }
                : new() { fallback };
        }

        // El serializador del framework responde en inglés cuando el JSON trae un valor que no se puede leer.
        private static string? Friendly(string field, string? message)
        {
            if (message is null) return null;
            var name = field.StartsWith("$.") ? field[2..] : field;
            if (message.StartsWith("The JSON value could not be converted") || message.Contains("could not be converted"))
                return $"El valor de \"{name}\" no es válido.";
            if (message.Contains("field is required") && field is "dto" or "")
                return "No se recibieron los datos del formulario.";
            if (message.StartsWith("A non-empty request body is required"))
                return "No se recibieron los datos del formulario.";
            return message;
        }
    }
}
