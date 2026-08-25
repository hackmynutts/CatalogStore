using Microsoft.AspNetCore.Identity;

namespace CatalogStore.BackendAPI.Models.Auth
{
    public class AuthModels
    {
        public record RegisterRequest(string UserName, string FullName, string Email, string Role, bool SendNotifications = false);
        public record LoginRequest(string Email, string Password);
        public record RegisterResult(bool Succeeded, IEnumerable<string> Errors, string? TemporaryPassword = null);
        public record LoginResult(bool Succeeded, string? Token, bool MustChangePassword = false);
        public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
        public record ChangePasswordResult(bool Succeeded, IEnumerable<string> Errors);
        public record ResetPasswordResult(bool Succeeded, IEnumerable<string> Errors, string? TemporaryPassword = null);
    }
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? FullName { get; set; }
        public bool SendNotifications { get; set; } = true;
    }
}
