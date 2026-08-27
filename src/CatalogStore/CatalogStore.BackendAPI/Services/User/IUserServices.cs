using CatalogStore.BackendAPI.DTO.User;
using CatalogStore.BackendAPI.Models.Auth;

namespace CatalogStore.BackendAPI.Services.User
{
    public interface IUserServices
    {
        Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request);
        Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request);
        Task<string?> RefreshTokenAsync(string oldToken);
        Task LogoutAsync(Guid userId);
        Task<AuthModels.ChangePasswordResult> ChangePasswordAsync(Guid userId, AuthModels.ChangePasswordRequest request);
        Task<AuthModels.ResetPasswordResult> ResetPasswordAsync(Guid id);
        Task<UserDTO?> GetUserAsync(Guid id);
        Task<List<UserDTO>> GetAllUsersAsync();
        Task<bool> UpdateUserAsync(Guid id, UpdateUserDTO request);
        Task<bool> ChangeRoleAsync(Guid id, string newRole);
        Task<bool> DeleteUserAsync(Guid id);
    }
}
