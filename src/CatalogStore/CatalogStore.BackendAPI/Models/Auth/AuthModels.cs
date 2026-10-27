using Microsoft.AspNetCore.Identity;

namespace CatalogStore.BackendAPI.Models.Auth
{
    public class AuthModels
    {
        public record RegisterRequest(string UserName, string FullName, string Email, string Role, bool SendNotifications = false);
        public record LoginRequest(string Email, string Password);
        // Cómo se entregó un enlace de contraseña: por correo (EmailSentTo) o, con el correo deshabilitado,
        // devuelto al administrador para que lo comparta (ManualLink).
        public record LinkDelivery(string? EmailSentTo, string? ManualLink);
        // Delivery null con Succeeded true: el usuario se creó pero la invitación no se pudo enviar (se reenvía después).
        public record RegisterResult(bool Succeeded, IEnumerable<string> Errors, LinkDelivery? Delivery = null);
        public record LoginResult(bool Succeeded, string? Token, bool MustChangePassword = false);
        public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
        public record ChangePasswordResult(bool Succeeded, IEnumerable<string> Errors);
        public record ResetPasswordResult(bool Succeeded, IEnumerable<string> Errors, LinkDelivery? Delivery = null);
        public record RefreshTokenRequest(string Token);

        // Enlaces de contraseña (invitación o reset). Mode: PasswordLinks.ModeInvite / ModeReset.
        public record ForgotPasswordRequest(string Email);
        public record PasswordLinkRequest(Guid UserId, string Token, string Mode);
        public record SetPasswordRequest(Guid UserId, string Token, string Mode, string NewPassword);
        public record PasswordLinkCheckResult(bool Valid, string? FullName = null, string? UserName = null, string? Message = null);
    }
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? FullName { get; set; }
        public bool SendNotifications { get; set; } = true;
    }
}
