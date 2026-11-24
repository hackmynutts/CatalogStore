using CatalogStore.BackendAPI.DTO.User;
using CatalogStore.BackendAPI.Models.Auth;

namespace CatalogStore.BackendAPI.Services.User
{
    public record UserActionResult(bool Succeeded, string? Error = null, bool NotFound = false);

    public interface IUserServices
    {
        // Crea el usuario sin contraseña y le envía la invitación para que la cree.
        Task<AuthModels.RegisterResult> RegisterAsync(AuthModels.RegisterRequest request, string actor);
        Task<AuthModels.LoginResult> LoginAsync(AuthModels.LoginRequest request);
        Task<string?> RefreshTokenAsync(string oldToken);
        Task LogoutAsync(Guid userId);
        Task<AuthModels.ChangePasswordResult> ChangePasswordAsync(Guid userId, AuthModels.ChangePasswordRequest request);
        // Administrador: envía al usuario un enlace para crear una contraseña nueva.
        Task<AuthModels.ResetPasswordResult> ResetPasswordAsync(Guid id, string actor);
        // Administrador: nueva invitación (la anterior deja de servir). Solo si el usuario todavía no tiene contraseña.
        Task<AuthModels.ResetPasswordResult> ResendInvitationAsync(Guid id, string actor);
        // Público: si el correo existe, encola el enlace. No revela si existe.
        Task ForgotPasswordAsync(string email);
        // Público: valida el enlace antes de mostrar el formulario.
        Task<AuthModels.PasswordLinkCheckResult> CheckPasswordLinkAsync(AuthModels.PasswordLinkRequest request);
        // Público: crea la contraseña con el enlace (invitación o reset).
        Task<AuthModels.ChangePasswordResult> SetPasswordFromLinkAsync(AuthModels.SetPasswordRequest request);
        Task<UserDTO?> GetUserAsync(Guid id);
        Task<List<UserDTO>> GetAllUsersAsync();
        Task<bool> UpdateUserAsync(Guid id, UpdateUserDTO request);
        // Cambiar rol, inactivar/reactivar y eliminar: devuelven el motivo si una regla lo impide (ver UserServices).
        Task<UserActionResult> ChangeRoleAsync(Guid id, string newRole, Guid actorId);
        Task<UserActionResult> SetActiveAsync(Guid id, bool active, Guid actorId);
        Task<UserActionResult> DeleteUserAsync(Guid id, Guid actorId);
    }
}
