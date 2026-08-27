using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace CatalogStore.UI.Handlers
{
    public class JwtDelegatingHandler : DelegatingHandler
    {
        private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(2);

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _httpClientFactory;

        public JwtDelegatingHandler(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var token = httpContext?.User?.FindFirst("access_token")?.Value;

            if (!string.IsNullOrEmpty(token) && httpContext != null)
            {
                token = await EnsureFreshTokenAsync(httpContext, token);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        // Si al token le quedan menos de RefreshWindow minutos (o ya venció), lo renueva contra
        // el Backend y reescribe la cookie con el nuevo — todo antes de que salga el request real.
        private async Task<string> EnsureFreshTokenAsync(HttpContext httpContext, string token)
        {
            JwtSecurityToken jwt;
            try
            {
                jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            }
            catch
            {
                return token;
            }

            if (jwt.ValidTo > DateTime.UtcNow.Add(RefreshWindow))
                return token;

            var authClient = _httpClientFactory.CreateClient("BackendApiAuth");
            var response = await authClient.PostAsJsonAsync("api/User/refresh-token", new { token });
            if (!response.IsSuccessStatusCode)
                return token;

            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDTO>();
            if (result == null || string.IsNullOrEmpty(result.Token))
                return token;

            var newJwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
            var claims = new List<Claim>(newJwt.Claims) { new Claim("access_token", result.Token) };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return result.Token;
        }
    }
}
