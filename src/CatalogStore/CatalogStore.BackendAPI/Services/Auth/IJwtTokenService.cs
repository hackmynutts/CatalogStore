using CatalogStore.BackendAPI.Data;

namespace CatalogStore.BackendAPI.Services.Auth
{
    public interface IJwtTokenService
    {
        string GenerateToken(ApplicationUser usuario, IList<string> roles);
    }
}
