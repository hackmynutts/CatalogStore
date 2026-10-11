using CatalogStore.UI.Handlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CatalogStore.UI.Filters
{
    // Si durante la acción la API rechazó el token (JwtDelegatingHandler lo marca), la sesión ya no sirve:
    // se cierra la cookie y se reemplaza el resultado. Sin esto, los controllers convertían el 401 en una lista vacía
    // y la pantalla parecía "sin datos" con la sesión aparentemente activa.
    //  - Navegación normal: redirige al login con un aviso.
    //  - fetch (los modales, las tablas por AJAX): responde 401 con JSON; session-guard.js redirige al login.
    public class SessionExpiredFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executed = await next();

            var http = context.HttpContext;
            if (!http.Items.ContainsKey(JwtDelegatingHandler.SessionExpiredKey))
                return;

            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Sec-Fetch-Mode lo envían los navegadores: "navigate" en navegación, "same-origin"/"cors" en fetch.
            var isNavigation = !http.Request.Headers.TryGetValue("Sec-Fetch-Mode", out var mode) || mode == "navigate";

            executed.ExceptionHandled = true;
            executed.Result = isNavigation
                ? new RedirectToActionResult("Login", "Account", new { expired = true })
                : new JsonResult(new { success = false, sessionExpired = true, message = "Tu sesión expiró. Iniciá sesión de nuevo." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
        }
    }
}
