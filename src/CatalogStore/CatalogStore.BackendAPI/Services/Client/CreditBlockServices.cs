using CatalogStore.BackendAPI.DTO.Client;
using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.Client;
using CatalogStore.BackendAPI.Repository.Sales;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.Orders;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Client
{
    // Bloqueo por mora: un cliente con Orders:BlockOverdueSalesFrom o más ventas vencidas no puede iniciar ni completar órdenes.
    // Se calcula en vivo cada vez (no hay un "está bloqueado" guardado): al pagar o anular la venta vencida se desbloquea solo.
    // Lo único que se guarda es la excepción que da un administrador (hasta una fecha y con motivo).
    public interface ICreditBlockServices
    {
        Task<CreditStatusDTO?> GetStatusAsync(int clientId);
        // null = puede operar; si no, el texto del bloqueo para mostrar al vendedor.
        Task<string?> BlockMessageAsync(int clientId);
        Task<OperationResult> SetOverrideAsync(int clientId, SetCreditOverrideDTO dto, string user);
        Task<OperationResult> ClearOverrideAsync(int clientId, string user);
    }

    public class CreditBlockServices : ICreditBlockServices
    {
        private const string ModuleName = "Ventas/Clientes";
        private const string ClientsTable = "Client";
        // Tope de duración de una excepción: pasado esto hay que volver a autorizarla.
        public const int MaxOverrideDays = 90;
        public const string NotFoundMessage = "Cliente no encontrado.";

        private readonly IClientRepository _clientRepository;
        private readonly ISalesRepository _salesRepository;
        private readonly IOptionsMonitor<OrdersOptions> _options;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;

        public CreditBlockServices(IClientRepository clientRepository, ISalesRepository salesRepository, IOptionsMonitor<OrdersOptions> options,
                                   IEventlogServices eventlogServices, TimeProvider time)
        {
            _clientRepository = clientRepository;
            _salesRepository = salesRepository;
            _options = options;
            _eventlogServices = eventlogServices;
            _time = time;
        }

        public async Task<CreditStatusDTO?> GetStatusAsync(int clientId)
        {
            var client = await _clientRepository.GetClientAsync(clientId);
            return client is null ? null : await BuildStatusAsync(client);
        }

        public async Task<string?> BlockMessageAsync(int clientId)
        {
            var status = await GetStatusAsync(clientId);
            return status is { IsBlocked: true } ? status.Message : null;
        }

        public async Task<OperationResult> SetOverrideAsync(int clientId, SetCreditOverrideDTO dto, string user)
        {
            var client = await _clientRepository.GetClientAsync(clientId);
            if (client is null)
                return OperationResult.Fail(NotFoundMessage);

            var reason = dto.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
                return OperationResult.Fail("El motivo es obligatorio.");
            if (dto.Until is null)
                return OperationResult.Fail("La fecha límite es obligatoria.");

            var today = TodayLocal();
            if (dto.Until.Value < today)
                return OperationResult.Fail("La fecha límite no puede ser anterior a hoy.");
            if (dto.Until.Value > today.AddDays(MaxOverrideDays))
                return OperationResult.Fail($"La excepción puede durar como máximo {MaxOverrideDays} días.");

            var before = new { client.CreditBlockOverrideUntil, client.CreditBlockOverrideReason, client.CreditBlockOverrideBy };
            client.CreditBlockOverrideUntil = dto.Until;
            client.CreditBlockOverrideReason = reason;
            client.CreditBlockOverrideBy = user;
            client.ModifiedBy = user;
            client.ModifiedOn = _time.GetUtcNow().UtcDateTime;

            if (!await _clientRepository.UpdateAsync(client))
                return OperationResult.Fail("No se pudo guardar la excepción.");

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, ClientsTable, clientId.ToString(),
                $"Excepción de bloqueo por mora autorizada hasta el {dto.Until.Value:dd/MM/yyyy}: {reason}",
                preData: before,
                postData: new { client.CreditBlockOverrideUntil, client.CreditBlockOverrideReason, client.CreditBlockOverrideBy });
            return OperationResult.Ok(clientId);
        }

        public async Task<OperationResult> ClearOverrideAsync(int clientId, string user)
        {
            var client = await _clientRepository.GetClientAsync(clientId);
            if (client is null)
                return OperationResult.Fail(NotFoundMessage);
            if (client.CreditBlockOverrideUntil is null)
                return OperationResult.Ok(clientId);

            var before = new { client.CreditBlockOverrideUntil, client.CreditBlockOverrideReason, client.CreditBlockOverrideBy };
            client.CreditBlockOverrideUntil = null;
            client.CreditBlockOverrideReason = null;
            client.CreditBlockOverrideBy = null;
            client.ModifiedBy = user;
            client.ModifiedOn = _time.GetUtcNow().UtcDateTime;

            if (!await _clientRepository.UpdateAsync(client))
                return OperationResult.Fail("No se pudo quitar la excepción.");

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, ClientsTable, clientId.ToString(),
                "Excepción de bloqueo por mora retirada.", preData: before);
            return OperationResult.Ok(clientId);
        }

        private async Task<CreditStatusDTO> BuildStatusAsync(Models.Client.Client client)
        {
            var threshold = Math.Max(0, _options.CurrentValue.BlockOverdueSalesFrom);
            var today = TodayLocal();
            var count = await _salesRepository.CountOverdueByClientAsync(client.Id, today);
            var reached = threshold > 0 && count >= threshold;
            var overrideActive = reached && client.CreditBlockOverrideUntil is { } until && until >= today;
            var blocked = reached && !overrideActive;

            return new CreditStatusDTO
            {
                ClientId = client.Id,
                OverdueCount = count,
                Threshold = threshold,
                IsBlocked = blocked,
                OverrideActive = overrideActive,
                // La excepción solo se muestra mientras sirve para algo: vigente y con el límite alcanzado.
                OverrideUntil = overrideActive ? client.CreditBlockOverrideUntil : null,
                OverrideReason = overrideActive ? client.CreditBlockOverrideReason : null,
                OverrideBy = overrideActive ? client.CreditBlockOverrideBy : null,
                Message = blocked
                    ? $"Cliente bloqueado por mora: tiene {count} ventas vencidas (el límite es {threshold}). Cobrá o regularizá esas ventas, o pedile a un administrador una excepción."
                    : null
            };
        }

        private DateOnly TodayLocal() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
    }
}
