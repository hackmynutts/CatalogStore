using System.Security.Claims;
using System.Threading.RateLimiting;
using CatalogStore.UI.Controllers;
using Microsoft.AspNetCore.RateLimiting;

namespace CatalogStore.UI.Handlers
{
    // Límites de peticiones de la UI (sección "RateLimiting" de appsettings). Aquí sí se ve la IP real de cada visitante,
    // por eso los límites de "puerta" (inicio de sesión, formularios públicos) viven en la UI. La API cuenta por usuario.
    public class UiRateLimitingOptions
    {
        public bool Enabled { get; set; } = true;
        // Tope general por persona (usuario con sesión; sin sesión, por IP). Páginas y acciones; los archivos estáticos no cuentan.
        public int ReadsPerMinute { get; set; } = 240;
        public int WritesPerMinute { get; set; } = 90;
        // Intentos de iniciar sesión por IP en 5 minutos (cuenta aciertos y fallos).
        public int LoginPer5Minutes { get; set; } = 10;
        // Generar un PDF consume CPU del servidor.
        public int PdfPerMinute { get; set; } = 20;
        // Acciones que mandan correos o administran usuarios.
        public int MailPerMinute { get; set; } = 10;
    }

    public static class UiRateLimitPolicies
    {
        public const string Login = "ui-login";
        public const string Pdf = "ui-pdf";
        public const string Mail = "ui-mail";
    }

    public static class UiRateLimiting
    {
        public static IServiceCollection AddUiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var o = configuration.GetSection("RateLimiting").Get<UiRateLimitingOptions>() ?? new UiRateLimitingOptions();

            services.AddRateLimiter(limiter =>
            {
                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                limiter.OnRejected = async (context, token) =>
                {
                    var http = context.HttpContext;
                    var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? (int)Math.Ceiling(after.TotalSeconds) : 60;
                    http.Response.Headers.RetryAfter = retry.ToString();
                    const string text = "Demasiadas solicitudes. Esperá un momento e intentá de nuevo.";
                    // Los navegadores piden páginas con Accept: text/html; fetch (modales, acciones) pide JSON.
                    if (http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        http.Response.ContentType = "text/plain; charset=utf-8";
                        await http.Response.WriteAsync(text, token);
                    }
                    else
                    {
                        http.Response.ContentType = "application/json";
                        await http.Response.WriteAsJsonAsync(new { success = false, message = text }, token);
                    }
                };

                // Tope general por persona: una bolsa para lecturas y otra para escrituras.
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    if (!o.Enabled) return RateLimitPartition.GetNoLimiter("off");
                    var write = !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method));
                    return Window($"g:{Who(context)}:{(write ? "w" : "r")}", write ? o.WritesPerMinute : o.ReadsPerMinute, TimeSpan.FromMinutes(1));
                });

                limiter.AddPolicy(UiRateLimitPolicies.Login, context =>
                    !o.Enabled ? RateLimitPartition.GetNoLimiter("off") : Window("login:" + Ip(context), o.LoginPer5Minutes, TimeSpan.FromMinutes(5)));
                limiter.AddPolicy(UiRateLimitPolicies.Pdf, context =>
                    !o.Enabled ? RateLimitPartition.GetNoLimiter("off") : Window("pdf:" + Who(context), o.PdfPerMinute, TimeSpan.FromMinutes(1)));
                limiter.AddPolicy(UiRateLimitPolicies.Mail, context =>
                    !o.Enabled ? RateLimitPartition.GetNoLimiter("off") : Window("mail:" + Who(context), o.MailPerMinute, TimeSpan.FromMinutes(1)));

                // Públicas (sin sesión), por IP: ya existían.
                limiter.AddPolicy(AccountController.PasswordFormsPolicy, context =>
                    !o.Enabled ? RateLimitPartition.GetNoLimiter("off") : RateLimitPartition.GetFixedWindowLimiter(Ip(context),
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
                limiter.AddPolicy(HomeController.ConsultationPolicy, context =>
                    !o.Enabled ? RateLimitPartition.GetNoLimiter("off") : RateLimitPartition.GetFixedWindowLimiter(Ip(context),
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });
            return services;
        }

        private static RateLimitPartition<string> Window(string key, int permits, TimeSpan window) =>
            RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permits),
                Window = window,
                SegmentsPerWindow = 5,
                QueueLimit = 0
            });

        private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Con sesión, la persona (su correo); sin sesión, su IP.
        private static string Who(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var id = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity.Name;
                if (!string.IsNullOrEmpty(id)) return "u:" + id;
            }
            return "ip:" + Ip(context);
        }
    }
}
