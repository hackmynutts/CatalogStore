namespace CatalogStore.BackendAPI.Services.Email
{
    // Un correo listo para encolar. HtmlBody ya armado (ver EmailTemplates).
    public record EmailMessage(
        string ToAddress,
        string? ToName,
        string Subject,
        string HtmlBody,
        string Kind,
        string? RelatedTable = null,
        string? RelatedID = null,
        bool IsSensitive = false);

    // Valores de EmailOutbox.Kind: identifican el tipo de correo en la cola.
    public static class EmailKinds
    {
        public const string Invitation = "Invitation";
        public const string PasswordReset = "PasswordReset";
        public const string PasswordChanged = "PasswordChanged";
    }

    // Encola correos. El envío real lo hace EmailOutboxWorker en segundo plano, con reintentos.
    public interface IEmailServices
    {
        // Email:Enabled. Con false nada sale; los flujos que dependen de un enlace se lo devuelven al administrador.
        bool Enabled { get; }
        // Agrega el correo al contexto SIN guardar: se guarda con el SaveChanges de quien llama (todo o nada).
        void Queue(EmailMessage message, string createdBy);
        // Agrega y guarda. Para flujos que no tienen otros cambios pendientes (ej. "olvidé mi contraseña").
        Task QueueAndSaveAsync(EmailMessage message, string createdBy);
        // Arma un enlace absoluto a una página de la UI con sus parámetros (ya codificados).
        string UiLink(string path, IDictionary<string, string> query);
        // ¿Se encoló un correo de este tipo para este registro dentro de la ventana? Sirve para no repetir envíos.
        Task<bool> WasQueuedRecentlyAsync(string kind, string relatedTable, string relatedId, TimeSpan window);
    }
}
