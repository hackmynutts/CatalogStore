using CatalogStore.BackendAPI.Models.EmailOutbox;

namespace CatalogStore.BackendAPI.Repository.Email
{
    public interface IEmailOutboxRepository
    {
        // No guarda: quien encola decide cuándo (junto con sus propios cambios o con SaveChangesAsync).
        void Add(EmailOutbox email);
        // Saca del contexto un correo que no se pudo guardar.
        void Detach(EmailOutbox email);
        // Pendientes cuyo próximo intento ya llegó, los más viejos primero. Con seguimiento: el worker los actualiza.
        Task<List<EmailOutbox>> GetDueAsync(DateTime now, int take);
        // ¿Hay un correo de este tipo para este registro creado desde 'since'? (cualquier estado)
        Task<bool> AnyQueuedSinceAsync(string kind, string relatedTable, string relatedId, DateTime since);
        Task<int> SaveChangesAsync();
    }
}
