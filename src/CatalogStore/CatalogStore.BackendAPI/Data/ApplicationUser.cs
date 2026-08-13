using Microsoft.AspNetCore.Identity;

namespace CatalogStore.BackendAPI.Data
{
    public sealed class ApplicationUser : IdentityUser<Guid>
    {
        public bool SendNotifications { get; set; }
        public string FullName { get; set; } = string.Empty;
    }
}
