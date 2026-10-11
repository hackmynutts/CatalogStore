using CatalogStore.UI.Models.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace CatalogStore.UI.Handlers
{
    public class JwtDelegatingHandler : DelegatingHandler
    {
        // Marca en HttpContext.Items: la API rechazó el token incluso después de renovarlo.
        // SessionExpiredFilter la lee al terminar la acción y manda al usuario a iniciar sesión.
        public const string SessionExpiredKey = "CatalogStore.SessionExpired";

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
            // Sin token (por ejemplo, el propio login) un 401 significa "credenciales inválidas", no "sesión vencida".
            var hadToken = !string.IsNullOrEmpty(token);

            if (hadToken && httpContext != null)
            {
                token = await EnsureFreshTokenAsync(httpContext, token!);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || !hadToken || httpContext == null)
                return response;

            // La API rechazó el token aunque el cliente lo creía vigente. Un intento de renovación forzada y,
            // si la petición se puede repetir (sin cuerpo, como un GET), un único reintento con el token nuevo.
            var renewed = await TryRefreshAsync(httpContext, token!);
            if (renewed != null)
            {
                if (request.Content != null)
                    return response; // la renovación funcionó, pero esta petición no se puede reenviar: el usuario la repite

                response.Dispose();
                var retry = CloneWithoutBody(request);
                retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed);
                response = await base.SendAsync(retry, cancellationToken);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                    return response;
            }

            httpContext.Items[SessionExpiredKey] = true;
            return response;
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

            return await TryRefreshAsync(httpContext, token) ?? token;
        }

        // Pide un token nuevo al Backend y reescribe la cookie. Devuelve el token nuevo, o null si no se pudo.
        private async Task<string?> TryRefreshAsync(HttpContext httpContext, string token)
        {
            try
            {
                var authClient = _httpClientFactory.CreateClient("BackendApiAuth");
                var response = await authClient.PostAsJsonAsync("api/User/refresh-token", new { token });
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponseDTO>();
                if (result == null || string.IsNullOrEmpty(result.Token))
                    return null;

                var newJwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
                var claims = new List<Claim>(newJwt.Claims) { new Claim("access_token", result.Token) };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                return result.Token;
            }
            catch (HttpRequestException)
            {
                // Backend caído: no es una sesión vencida; la petición original ya mostrará su propio error.
                return null;
            }
        }

        // Un HttpRequestMessage no se puede enviar dos veces: se copia método, URL y encabezados.
        private static HttpRequestMessage CloneWithoutBody(HttpRequestMessage original)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
            foreach (var header in original.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    continue;
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return clone;
        }
    }
}
