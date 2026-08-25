namespace CatalogStore.BackendAPI.DTO.User
{
    public class UpdateUserDTO
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool SendNotifications { get; set; }
    }
}
