namespace CatalogStore.UI.Models.User
{
    public class UpdateUserViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool SendNotifications { get; set; }
    }
}
