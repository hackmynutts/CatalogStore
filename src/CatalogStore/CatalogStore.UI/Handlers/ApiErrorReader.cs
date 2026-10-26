using System.Net;
using System.Text.Json;

namespace CatalogStore.UI.Handlers
{
    // Convierte una respuesta de error de la API en un mensaje legible para mostrar al usuario.
    // La API responde { message } en reglas de negocio y ProblemDetails { errors: { campo: [..] } } en validaciones.
    public static class ApiErrorReader
    {
        public static async Task<string> ReadAsync(HttpResponseMessage response, string fallback)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return "No tenés permiso para esta acción.";
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString() ?? fallback;
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var list = errors.EnumerateObject()
                        .SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString()))
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    var joined = string.Join(" ", list);
                    if (joined.Length > 0)
                        return joined;
                }
            }
            catch (JsonException) { }
            return fallback;
        }
    }
}
