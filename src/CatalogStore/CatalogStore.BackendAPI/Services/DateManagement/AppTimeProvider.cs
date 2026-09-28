namespace CatalogStore.BackendAPI.Services.DateManagement
{
    /// <summary>
    /// Reloj de la aplicación. La hora UTC es la del sistema; la zona "local" es la configurada en
    /// Localization:TimeZoneId (Costa Rica), sin importar la zona del servidor donde corra la API.
    /// Regla: en la base de datos se guarda UTC (GetUtcNow); la hora local solo se usa para mostrar.
    /// </summary>
    public class AppTimeProvider : TimeProvider
    {
        private readonly TimeZoneInfo _zone;

        public AppTimeProvider(IConfiguration configuration)
        {
            var zoneId = configuration["Localization:TimeZoneId"];
            if (string.IsNullOrWhiteSpace(zoneId))
                throw new InvalidOperationException("Localization:TimeZoneId no está configurado.");

            _zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }

        public override TimeZoneInfo LocalTimeZone => _zone;

        /// <summary>Convierte una fecha guardada en UTC (como las que devuelve EF) a la hora local configurada.</summary>
        public DateTime ToLocal(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
    }
}
