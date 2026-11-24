using System.Collections.Concurrent;

namespace CatalogStore.BackendAPI.Services.Security
{
    // Freno al "adivinar contraseñas": cuenta los inicios de sesión fallidos por correo. Con LoginMaxFailures fallos seguidos
    // el correo queda en pausa LoginBlockMinutes minutos (aunque después ponga la contraseña correcta); un acierto borra la cuenta.
    // Es por correo y no por IP porque a la API siempre llega la IP de la UI. Vive en memoria: un reinicio del servidor la limpia,
    // y es suficiente para frenar a un robot (que necesita miles de intentos), no para un descuido humano.
    public class LoginThrottle
    {
        private record Entry(int Failures, DateTimeOffset First, DateTimeOffset? BlockedUntil);

        private readonly ConcurrentDictionary<string, Entry> _entries = new();
        private readonly RateLimitingOptions _options;
        private readonly TimeProvider _time;

        public LoginThrottle(RateLimitingOptions options, TimeProvider time)
        {
            _options = options;
            _time = time;
        }

        private static string Key(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();
        private TimeSpan Window => TimeSpan.FromMinutes(Math.Max(1, _options.LoginBlockMinutes));

        // true si el correo está en pausa; retryAfter dice cuánto falta.
        public bool IsBlocked(string? email, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            if (!_options.Enabled || !_entries.TryGetValue(Key(email), out var entry) || entry.BlockedUntil is not { } until) return false;
            var left = until - _time.GetUtcNow();
            if (left <= TimeSpan.Zero) { _entries.TryRemove(Key(email), out _); return false; }
            retryAfter = left;
            return true;
        }

        public void RecordFailure(string? email)
        {
            if (!_options.Enabled || string.IsNullOrWhiteSpace(email)) return;
            var now = _time.GetUtcNow();
            _entries.AddOrUpdate(Key(email),
                _ => new Entry(1, now, null),
                (_, old) =>
                {
                    // Los fallos viejos (fuera de la ventana) no cuentan.
                    var failures = now - old.First > Window ? 1 : old.Failures + 1;
                    var first = now - old.First > Window ? now : old.First;
                    return failures >= Math.Max(1, _options.LoginMaxFailures) ? new Entry(failures, first, now + Window) : new Entry(failures, first, null);
                });
        }

        public void Reset(string? email) => _entries.TryRemove(Key(email), out _);
    }
}
