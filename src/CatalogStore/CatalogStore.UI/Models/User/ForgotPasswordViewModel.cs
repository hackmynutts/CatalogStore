using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.User
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Ingresá tu correo.")]
        [EmailAddress(ErrorMessage = "El correo no es válido.")]
        public string Email { get; set; } = string.Empty;
    }
}
