using CatalogStore.BackendAPI.Models.Auth;

namespace CatalogStore.BackendAPI.Services.User
{
    public interface IUserServices
    {
        Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request);
        Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request);
    }
}
