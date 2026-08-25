using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.User
{
    public class RegisterViewModel
    {
        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public bool SendNotifications { get; set; }
    }
}
