using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogStore.BackendAPI.Services.Security
{
    // Límites de peticiones de la API (sección "RateLimiting" de la configuración). Cuántas veces por minuto puede llamar cada persona.
    //
    // La API la llama siempre la UI (servidor a servidor), así que la IP que ve es la de la UI: no sirve para distinguir personas.
    // Por eso aquí se cuenta POR USUARIO (el "sub" del token). Los límites por IP real viven en la UI.
    public class RateLimitingOptions
    {
        // false: sin límites (solo para pruebas locales).
        public bool Enabled { get; set; } = true;
        // Tope general por usuario. Las lecturas (GET) y las escrituras (POST/PUT/DELETE) tienen cada una su propia bolsa.
        public int ReadsPerMinute { get; set; } = 300;
        public int WritesPerMinute { get; set; } = 90;
        // Acciones que mandan correos o administran usuarios.
        public int SensitivePerMinute { get; set; } = 10;
        // Emitir factura, nota de crédito, actualizar estado: hablan con el proveedor y con Hacienda.
        public int InvoicingPerMinute { get; set; } = 20;
        // Reportes en Excel e importación del catálogo del proveedor: consumen mucho servidor.
        public int HeavyPerMinute { get; set; } = 20;
        // Consultas a servicios externos (Hacienda, CABYS).
        public int ExternalPerMinute { get; set; } = 30;
        // Endpoints sin sesión (olvidé mi contraseña, enlaces): una sola bolsa para todos, porque todos llegan con la IP de la UI.
        public int AnonymousPerMinute { get; set; } = 30;
        // Intentos de inicio de sesión fallidos por correo antes de pausar ese correo, y cuánto dura la pausa.
        public int LoginMaxFailures { get; set; } = 8;
        public int LoginBlockMinutes { get; set; } = 15;
    }

    // Nombres de las políticas para [EnableRateLimiting(...)] en los controladores.
    public static class RateLimitPolicies
    {
        public const string Sensitive = "Sensitive";
        public const string Invoicing = "Invoicing";
        public const string Heavy = "Heavy";
        public const string External = "External";
        public const string Anonymous = "Anonymous";
    }

    public static class ApiRateLimiting
    {
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new RateLimitingOptions();
            services.AddSingleton(options);

            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.OnRejected = async (context, token) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                    else
                        context.HttpContext.Response.Headers.RetryAfter = "60";
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync("{\"message\":\"Demasiadas solicitudes. Esperá un momento e intentá de nuevo.\"}", token);
                };

                // Tope general: cada usuario tiene una bolsa para lecturas y otra para escrituras.
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (!options.Enabled) return RateLimitPartition.GetNoLimiter("off");
                    var write = !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method));
                    var user = UserKey(context);
                    // Sin sesión (login, catálogo público...): una bolsa compartida, más grande, porque llega todo con la IP de la UI.
                    if (user is null) return Window("anon:" + (write ? "w" : "r"), (write ? options.WritesPerMinute : options.ReadsPerMinute) * 4);
                    return Window($"user:{user}:{(write ? "w" : "r")}", write ? options.WritesPerMinute : options.ReadsPerMinute);
                });

                void PerUser(string name, int perMinute) => limiter.AddPolicy(name, context =>
                    !options.Enabled ? RateLimitPartition.GetNoLimiter("off") : Window($"{name}:{UserKey(context) ?? "anon"}", perMinute));

                PerUser(RateLimitPolicies.Sensitive, options.SensitivePerMinute);
                PerUser(RateLimitPolicies.Invoicing, options.InvoicingPerMinute);
                PerUser(RateLimitPolicies.Heavy, options.HeavyPerMinute);
                PerUser(RateLimitPolicies.External, options.ExternalPerMinute);
                limiter.AddPolicy(RateLimitPolicies.Anonymous, _ =>
                    !options.Enabled ? RateLimitPartition.GetNoLimiter("off") : Window("anonymous-endpoints", options.AnonymousPerMinute));
            });
            return services;
        }

        // Ventana deslizante de un minuto dividida en 4 tramos: más pareja que una ventana fija (no deja pasar el doble en el borde).
        private static RateLimitPartition<string> Window(string key, int permits) =>
            RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permits),
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueLimit = 0
            });

        private static string? UserKey(HttpContext context) =>
            context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value : null;
    }
}
