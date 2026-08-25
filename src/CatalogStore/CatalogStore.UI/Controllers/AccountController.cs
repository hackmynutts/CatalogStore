using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CatalogStore.UI.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        [HttpGet]
        public ActionResult Login()
        {
            return View();
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
                ViewData["ChangePasswordError"] = "No se pudo cambiar la contraseña. Verificá que la actual sea correcta.";
                return View(model);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> Details()
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            var response = await client.GetAsync("api/User/me");

            if (!response.IsSuccessStatusCode)
                return RedirectToAction("Index", "Home");

            var user = await response.Content.ReadFromJsonAsync<UserViewModel>();
            if (user == null)
                return RedirectToAction("Index", "Home");

            return View(user);
        }
    }
}
