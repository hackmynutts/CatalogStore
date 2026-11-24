namespace CatalogStore.BackendAPI.DTO.User
{
    public class UserDTO
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool SendNotifications { get; set; }
        // false: invitado que todavía no creó su contraseña.
        public bool HasPassword { get; set; }
        // false: el administrador lo inactivó; no puede iniciar sesión ni usar el sistema.
        public bool IsActive { get; set; } = true;
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
