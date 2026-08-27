using CatalogStore.BackendAPI.Data;

namespace CatalogStore.BackendAPI.Services.Auth
{
    public interface IJwtTokenService
    {
        string GenerateToken(ApplicationUser usuario, IList<string> roles);

        /// <summary>
        /// Valida la firma/issuer/audience de un token sin exigir que siga vigente — para refrescarlo.
        /// Devuelve el Id del usuario (claim "sub") si la firma es válida, o null si el token fue
        /// alterado, es de otro issuer, o está corrupto.
        /// </summary>
        Guid? GetUserIdFromExpiredToken(string token);
    }
}
