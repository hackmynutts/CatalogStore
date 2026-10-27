using CatalogStore.UI.Handlers;
using CatalogStore.UI.Models.Goals;
using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CatalogStore.UI.Controllers
{
    public class AccountController : Controller
    {
        // Límite por IP para "olvidé mi contraseña" (ver Program.cs).
        public const string PasswordFormsPolicy = "password-forms";

        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        [HttpGet]
        public ActionResult Login(bool expired = false, bool passwordSet = false)
        {
            // Llega con ?expired=true cuando SessionExpiredFilter o session-guard.js detectan que el token ya no sirve.
            if (expired)
                ViewData["LoginError"] = "Tu sesión expiró. Iniciá sesión de nuevo para continuar.";
            // Llega con ?passwordSet=true después de crear la contraseña con el enlace del correo.
            if (passwordSet)
                ViewData["LoginNotice"] = "Tu contraseña quedó lista. Iniciá sesión con ella.";
            return View();
        }

        // ───────── Olvidé mi contraseña y enlaces del correo (públicos) ─────────
        // Usan el cliente BackendApiAuth: no hay sesión, así que no hace falta el JwtDelegatingHandler.

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(PasswordFormsPolicy)]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _httpClientFactory.CreateClient("BackendApiAuth");
                var response = await client.PostAsJsonAsync("api/User/forgot-password", new { model.Email });
                if (!response.IsSuccessStatusCode)
                {
                    ViewData["FormError"] = "No se pudo procesar la solicitud. Probá de nuevo en unos minutos.";
                    return View(model);
                }
            }
            catch (HttpRequestException)
            {
                ViewData["FormError"] = "No se pudo conectar con el servidor. Probá de nuevo en unos minutos.";
                return View(model);
            }

            // Siempre la misma respuesta, exista o no el correo.
            ViewData["Sent"] = true;
            return View(model);
        }

        [HttpGet]
        public async Task<ActionResult> SetPassword(Guid uid, string? token, string? mode)
        {
            var model = new SetPasswordViewModel
            {
                UserId = uid,
                Token = token ?? string.Empty,
                Mode = mode == SetPasswordViewModel.ModeInvite ? SetPasswordViewModel.ModeInvite : SetPasswordViewModel.ModeReset
            };

            var check = await CheckLinkAsync(model);
            if (check == null || !check.Valid)
            {
                ViewData["LinkError"] = check?.Message ?? "No se pudo validar el enlace. Probá de nuevo en unos minutos.";
                return View(model);
            }

            model.FullName = check.FullName;
            model.UserName = check.UserName;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Volver a llenar el saludo: no viaja en el formulario.
                var check = await CheckLinkAsync(model);
                model.FullName = check?.FullName;
                model.UserName = check?.UserName;
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("BackendApiAuth");
                var response = await client.PostAsJsonAsync("api/User/password-link", new
                {
                    model.UserId,
                    model.Token,
                    model.Mode,
                    model.NewPassword
                });

                if (!response.IsSuccessStatusCode)
                {
                    ViewData["FormError"] = await ApiErrorReader.ReadAsync(response, "No se pudo guardar la contraseña.");
                    var check = await CheckLinkAsync(model);
                    if (check == null || !check.Valid)
                        ViewData["LinkError"] = check?.Message ?? ViewData["FormError"];
                    model.FullName = check?.FullName;
                    model.UserName = check?.UserName;
                    return View(model);
                }
            }
            catch (HttpRequestException)
            {
                ViewData["FormError"] = "No se pudo conectar con el servidor. Probá de nuevo en unos minutos.";
                return View(model);
            }

            // Si había otra sesión abierta en este navegador, se cierra: la próxima entrada es con la contraseña nueva.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login), new { passwordSet = true });
        }

        // null: el backend no respondió.
        private async Task<PasswordLinkCheckDTO?> CheckLinkAsync(SetPasswordViewModel model)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BackendApiAuth");
                var response = await client.PostAsJsonAsync("api/User/password-link/check", new { model.UserId, model.Token, model.Mode });
                if (!response.IsSuccessStatusCode)
                    return null;
                return await response.Content.ReadFromJsonAsync<PasswordLinkCheckDTO>();
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var resp = await client.PostAsJsonAsync("api/User/login",
                                                    new { model.email, model.password });
            //validacion 
            if (!resp.IsSuccessStatusCode)
            {
                ViewData["LoginError"] = "Email o contraseña incorrectos.";
                return View(model);
            }

            var result = await resp.Content.ReadFromJsonAsync<LoginResponseDTO>();
            if (result == null)
            {
                ViewData["LoginError"] = "Ocurrió un error inesperado al iniciar sesión.";
                return View(model);
            }
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwt = handler.ReadJwtToken(result.Token);

            var claims = new List<Claim>(jwt.Claims);
            claims.Add(new Claim("access_token", result.Token));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (result.MustChangePassword)
                return RedirectToAction("ChangePassword", "Account");

            return RedirectToAction("Index", "Home"); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Logout()
        {
            try
            {
                // Tiene que ir antes del SignOutAsync — una vez cerrada la cookie ya no queda
                // JWT para autenticar esta llamada. Si el Backend no responde, no bloquea el logout.
                var client = _httpClientFactory.CreateClient("BackendApi");
                await client.PostAsync("api/User/logout", null);
            }
            catch
            {
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [Authorize]
        public ActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.PostAsJsonAsync("api/User/change-password", new
            {
                model.CurrentPassword,
                model.NewPassword
            });

            if (!response.IsSuccessStatusCode)
            {
                ViewData["ChangePasswordError"] = await ApiErrorReader.ReadAsync(response,
                    "No se pudo cambiar la contraseña. Verificá que la actual sea correcta.");
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> Details(int? year, int? month)
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/User/me");

            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Index", "Home");

            var user = await response.Content.ReadFromJsonAsync<UserViewModel>();
            if (user == null)
                return RedirectToAction("Index", "Home");

            // Metas del mes (vendedores y administradores). Si la API no responde, el perfil se muestra igual sin ellas.
            if (user.Roles.Any(r => r is "Vendedor" or "Admin" or "AdminIT"))
            {
                try
                {
                    var period = year.HasValue && month.HasValue ? $"?year={year.Value}&month={month.Value}" : string.Empty;
                    var goals = await client.GetAsync("api/Goal/me" + period);
                    if (goals.IsSuccessStatusCode && await goals.Content.ReadFromJsonAsync<MyGoalsViewModel>() is { } mine)
                    {
                        var history = await client.GetAsync("api/Goal/me/history");
                        if (history.IsSuccessStatusCode)
                            mine.History = await history.Content.ReadFromJsonAsync<List<GoalHistoryItem>>() ?? new();
                        ViewData["Goals"] = mine;
                    }
                }
                catch (HttpRequestException) { }
            }

            return View(user);
        }
    }
}
