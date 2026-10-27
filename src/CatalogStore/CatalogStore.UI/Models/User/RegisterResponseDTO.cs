namespace CatalogStore.UI.Models.User
{
    // Respuesta de crear usuario, restablecer contraseña y reenviar invitación.
    public class RegisterResponseDTO
    {
        public string? Message { get; set; }
        // Correo al que se envió el enlace.
        public string? EmailSentTo { get; set; }
        // Solo con el correo deshabilitado: el administrador comparte el enlace a mano.
        public string? ManualLink { get; set; }
        // La acción se hizo, pero el enlace no se pudo enviar (se reintenta desde la lista).
        public bool DeliveryFailed { get; set; }
    }
}
