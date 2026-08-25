using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.User
{
    public class LoginViewModel
    {
        [Required]
        public string email { get; set; } 
        [Required]
        public string password { get; set; }
    }
}
