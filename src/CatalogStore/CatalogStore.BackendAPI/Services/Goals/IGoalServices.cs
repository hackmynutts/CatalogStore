using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.Goals;

namespace CatalogStore.BackendAPI.Services.Goals
{
    public interface IGoalServices
    {
        // year/month null = mes en curso (hora local). Quien llama valida el período con GoalServices.PeriodError.
        Task<MyProgressDTO> GetMyProgressAsync(string user, int? year, int? month);
        // Últimos 6 meses, el más reciente primero.
        Task<List<GoalHistoryDTO>> GetMyHistoryAsync(string user);
        // Las ventas y los abonos que suman en el avance del mes del propio usuario (el detalle de las tarjetas).
        Task<MyGoalDetailDTO> GetMyDetailAsync(string user, int? year, int? month);
        // Todos los vendedores (con o sin meta) y cualquier otro usuario con meta o movimiento ese mes.
        Task<TeamProgressDTO> GetTeamProgressAsync(int? year, int? month);
        // Crea o actualiza la meta de un vendedor en un mes. Solo del mes en curso en adelante.
        Task<OperationResult> SetGoalAsync(SetGoalDTO dto, string user);
        // Copia las metas del mes anterior a los vendedores que todavía no tienen meta en el mes pedido. Id = cuántas copió.
        Task<OperationResult> CopyPreviousMonthAsync(CopyGoalsDTO dto, string user);
    }
}
