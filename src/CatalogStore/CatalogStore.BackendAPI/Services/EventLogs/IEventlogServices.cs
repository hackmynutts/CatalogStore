using CatalogStore.BackendAPI.DTO.EventLogs;
using CatalogStore.BackendAPI.Models.EventLogs;

namespace CatalogStore.BackendAPI.Services.EventLogs
{
    public interface IEventlogServices
    {
        Task<List<Eventlog>> GetAllEventsAsync();
        Task AddAsync(AddEventlogDTO eventlog);

        /// <summary>
        /// Punto único de auditoría para todo el sistema. Cualquier Service (User, Status, o futuros
        /// módulos) llama esto en vez de armar un AddEventlogDTO a mano. Resuelve el "quién" (claim del
        /// usuario autenticado) y serializa preData/postData internamente.
        /// </summary>
        /// <param name="actorOverride">
        /// Usar solo cuando todavía no hay usuario autenticado en el HttpContext (ej: un login fallido,
        /// donde el request es anónimo). Si se pasa, reemplaza el claim del usuario logueado como CreatedBy.
        /// </param>
        Task LogAsync(
            typeEvent type,
            string moduleName,
            string tableName,
            string recordId,
            string description,
            object? preData = null,
            object? postData = null,
            string? stackTrace = null,
            string? actorOverride = null);
    }
}
