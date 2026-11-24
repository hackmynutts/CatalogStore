using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CatalogStore.BackendAPI.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace CatalogStore.BackendAPI.Services.Security
{
    // Un token firmado sigue siendo válido hasta que vence, aunque la persona ya no exista, la hayan inactivado o le hayan cambiado el rol.
    // Esta guarda revisa en cada petición (con una copia de 20 segundos para no ir a la base cada vez) que el usuario siga activo y con
    // los mismos roles que dice el token. Si no, la petición se rechaza con 401 y la UI lo manda a iniciar sesión otra vez con sus datos nuevos.
    public static class UserSessionGuard
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(20);
        private record State(bool Exists, bool Active, string Roles);

        // Un usuario inactivo tiene la fecha de fin de bloqueo "para siempre" (los bloqueos normales no llegan ni cerca).
        public static bool IsInactive(ApplicationUser user) =>
            user.LockoutEnd is { } end && end > DateTimeOffset.UtcNow.AddYears(50);

        private static string Key(Guid id) => $"session-state:{id}";

        public static void Invalidate(IMemoryCache cache, Guid userId) => cache.Remove(Key(userId));

        public static async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, IServiceProvider services)
        {
            if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)) return false;

            var cache = services.GetRequiredService<IMemoryCache>();
            if (!cache.TryGetValue(Key(id), out State? state) || state is null)
            {
                var users = services.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(id.ToString());
                state = user is null
                    ? new State(false, false, string.Empty)
                    : new State(true, !IsInactive(user), Normalize(await users.GetRolesAsync(user)));
                cache.Set(Key(id), state, CacheFor);
            }
            if (!state.Exists || !state.Active) return false;
            return Normalize(principal.FindAll(ClaimTypes.Role).Select(c => c.Value)) == state.Roles;
        }

        private static string Normalize(IEnumerable<string> roles) =>
            string.Join(",", roles.Select(r => r.ToUpperInvariant()).OrderBy(r => r, StringComparer.Ordinal));
    }
}
