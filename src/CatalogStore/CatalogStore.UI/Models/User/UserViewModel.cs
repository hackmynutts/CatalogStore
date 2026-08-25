namespace CatalogStore.UI.Models.User
{
    public class UserViewModel
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool SendNotifications { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
