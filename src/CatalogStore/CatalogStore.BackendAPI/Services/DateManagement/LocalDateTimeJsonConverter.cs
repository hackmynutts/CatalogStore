using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatalogStore.BackendAPI.Services.DateManagement
{
    /// <summary>
    /// Contrato de fechas de la API: la base de datos guarda UTC, pero el JSON que entra y sale usa
    /// la hora local configurada (Costa Rica), sin desfase ("2026-09-27T23:04:56").
    /// - Al escribir: convierte UTC a hora local.
    /// - Al leer: una fecha sin desfase se interpreta como hora local y se convierte a UTC;
    ///   una con "Z" o con desfase explícito se respeta y se normaliza a UTC.
    /// System.Text.Json aplica este convertidor también a DateTime? automáticamente.
    /// </summary>
    public class LocalDateTimeJsonConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-ddTHH:mm:ss.FFFFFFF";
        private readonly AppTimeProvider _time;

        public LocalDateTimeJsonConverter(AppTimeProvider time)
        {
            _time = time;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // Los extremos (por ejemplo, un default(DateTime)) no se convierten: restar 6 horas a MinValue se sale de rango.
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
            {
                writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
                return;
            }

            var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
            writer.WriteStringValue(_time.ToLocal(utc).ToString(Format, CultureInfo.InvariantCulture));
        }

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text))
                throw new JsonException("Fecha vacía.");

            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                throw new JsonException($"Fecha inválida: '{text}'.");

            if (parsed == DateTime.MinValue || parsed == DateTime.MaxValue)
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

            return parsed.Kind switch
            {
                DateTimeKind.Utc => parsed,
                DateTimeKind.Local => parsed.ToUniversalTime(),
                _ => TimeZoneInfo.ConvertTimeToUtc(parsed, _time.LocalTimeZone)
            };
        }
    }
}
