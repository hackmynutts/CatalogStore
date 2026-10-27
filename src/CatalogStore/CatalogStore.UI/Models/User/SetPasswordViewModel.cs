using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.User
{
    // Formulario del enlace que llega por correo (invitación o restablecer contraseña).
    public class SetPasswordViewModel
    {
        public const string ModeInvite = "invite";
        public const string ModeReset = "reset";

        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string Mode { get; set; } = ModeReset;

        // Solo para mostrar; los llena la verificación del enlace.
        public string? FullName { get; set; }
        public string? UserName { get; set; }

        [Required(ErrorMessage = "Ingresá la contraseña nueva.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmá la contraseña.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool IsInvite => Mode == ModeInvite;
    }

    public class PasswordLinkCheckDTO
    {
        public bool Valid { get; set; }
        public string? FullName { get; set; }
        public string? UserName { get; set; }
        public string? Message { get; set; }
    }
}
