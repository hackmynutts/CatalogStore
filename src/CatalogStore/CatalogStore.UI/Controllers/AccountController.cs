using CatalogStore.UI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }
    }
}
