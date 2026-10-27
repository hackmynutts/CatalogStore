using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.EmailOutbox
{
    public enum EmailStatus
    {
        [Display(Name = "Pendiente")]
        Pending = 0,
        [Display(Name = "Enviado")]
        Sent = 1,
        // Agotó los reintentos: queda para revisión, no se vuelve a intentar solo.
        [Display(Name = "Fallido")]
        Failed = 2
    }

    // Cola de correos: quien necesita mandar un correo lo deja aquí y EmailOutboxWorker lo envía en segundo plano.
    // Así un SMTP caído no hace fallar la acción del usuario, se reintenta solo y queda constancia de cada envío.
    [Table("EmailOutbox_TB")]
    public class EmailOutbox
    {
        public int EmailOutboxID { get; set; }
        public string ToAddress { get; set; } = string.Empty;
        public string? ToName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        // Qué correo es (ej. "PasswordReset", "Invitation"): para filtrar y reportar sin leer el cuerpo.
        public string Kind { get; set; } = string.Empty;
        // A qué registro pertenece (ej. "User" + id, "SalesHeader" + id).
        public string? RelatedTable { get; set; }
        public string? RelatedID { get; set; }
        // Lleva un enlace con token (reset, invitación): el cuerpo se borra al enviarse o fallar,
        // para que la base no guarde enlaces que alguien podría usar.
        public bool IsSensitive { get; set; }
        public EmailStatus Status { get; set; } = EmailStatus.Pending;
        public int Attempts { get; set; }
        public DateTime NextAttemptOn { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentOn { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
