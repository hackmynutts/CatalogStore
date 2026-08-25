using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.User
{
    public class LoginResponseDTO
    {
        public string Token { get; set; }
        public bool MustChangePassword { get; set; }
    }
}
