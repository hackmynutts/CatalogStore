using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.DateManagement
{
    /// <summary>
    /// Agrega el convertidor de fechas locales al JSON de los controllers. Se hace con IConfigureOptions
    /// para poder recibir AppTimeProvider por inyección (en AddJsonOptions todavía no existe el contenedor).
    /// </summary>
    public class ConfigureLocalDateTimeJson : IConfigureOptions<JsonOptions>
    {
        private readonly AppTimeProvider _time;

        public ConfigureLocalDateTimeJson(AppTimeProvider time)
        {
            _time = time;
        }

        public void Configure(JsonOptions options) =>
            options.JsonSerializerOptions.Converters.Add(new LocalDateTimeJsonConverter(_time));
    }
}
