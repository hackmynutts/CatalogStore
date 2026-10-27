namespace CatalogStore.BackendAPI.Services.Email
{
    // Sección "Email" de la configuración. La contraseña NUNCA va en appsettings: user-secrets en desarrollo y
    // configuración de App Service (Email__Password) en el servidor.
    // Gmail / Google Workspace: Host smtp.gmail.com, Port 587, UseStartTls true, UserName = la cuenta completa y
    // Password = una "contraseña de aplicación" (requiere verificación en dos pasos), no la contraseña normal.
    public class EmailOptions
    {
        // false: los correos se encolan pero no se envían (útil en desarrollo sin SMTP).
        public bool Enabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        // Con Gmail tiene que ser la misma cuenta de UserName o un alias "Enviar como" verificado en esa cuenta.
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "SAM DESIGN";
        // Dirección pública de la UI: los enlaces de los correos (restablecer contraseña, etc.) apuntan aquí.
        public string UiBaseUrl { get; set; } = string.Empty;
        // Intentos antes de marcar un correo como fallido.
        public int MaxAttempts { get; set; } = 5;
        // Cada cuántos segundos el worker revisa la cola.
        public int PollSeconds { get; set; } = 15;
    }
}
