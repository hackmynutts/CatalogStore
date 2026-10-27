using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.Email;
using CatalogStore.BackendAPI.Services.EventLogs;

namespace CatalogStore.BackendAPI.Services.Orders
{
    public static class Proforma
    {
        // Días que el cliente tiene para aceptar el precio. Lo usa también la pantalla imprimible de la UI.
        public const int ValidDays = 8;

        // La proforma toma el número de su orden (SAM0000123 → PRO-0000123): no es un documento aparte con su propia serie.
        // Si la orden cambia, la proforma que se imprima después refleja los cambios con la misma numeración.
        public static string Number(string orderNumber)
        {
            var digits = new string((orderNumber ?? string.Empty).Where(char.IsDigit).ToArray());
            return "PRO-" + (digits.Length == 0 ? orderNumber : digits);
        }
    }

    public interface IProformaServices
    {
        // Manda al correo del cliente la proforma de la orden (en el cuerpo del correo, no como adjunto).
        Task<OperationResult> SendByEmailAsync(int orderId, string user, bool isAdmin);
    }

    // La proforma es una cotización: no fiscal, no mueve stock ni genera venta. Solo se arma de una orden abierta.
    public class ProformaServices : IProformaServices
    {
        private const string ModuleName = "Ventas/Órdenes";
        private readonly IOrderServices _orderServices;
        private readonly IClientServices _clientServices;
        private readonly IEmailServices _emailServices;
        private readonly IEventlogServices _eventlogServices;
        private readonly TimeProvider _time;

        public ProformaServices(IOrderServices orderServices, IClientServices clientServices, IEmailServices emailServices,
            IEventlogServices eventlogServices, TimeProvider time)
        {
            _orderServices = orderServices;
            _clientServices = clientServices;
            _emailServices = emailServices;
            _eventlogServices = eventlogServices;
            _time = time;
        }

        public async Task<OperationResult> SendByEmailAsync(int orderId, string user, bool isAdmin)
        {
            // GetAsync ya aplica la regla de que el vendedor solo ve sus órdenes.
            var order = await _orderServices.GetAsync(orderId, user, isAdmin);
            if (order is null) return OperationResult.Fail(OrderServices.NotFoundMessage);
            if (order.OrderStatus is not (Models.Order.OrderStatus.InProcess or Models.Order.OrderStatus.Pending))
                return OperationResult.Fail("La proforma es para órdenes abiertas (en proceso o pendientes).");
            if (order.Lines.Count == 0) return OperationResult.Fail("La orden no tiene productos.");
            if (!_emailServices.Enabled) return OperationResult.Fail("El envío de correos está deshabilitado (Email:Enabled).");

            var client = await _clientServices.GetClientAsync(order.ClientID);
            if (client is null || string.IsNullOrWhiteSpace(client.ClientEmail))
                return OperationResult.Fail("El cliente no tiene correo registrado.");

            var number = Proforma.Number(order.OrderNumber);
            var issued = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _time.LocalTimeZone).DateTime;
            var discount = order.Lines.Sum(l => l.UnitPrice * l.Quantity) - order.Subtotal;
            var lines = order.Lines.Select(l => new EmailTemplates.ProformaLine(l.ProductCode, l.ProductName, l.Quantity, l.UnitPrice, l.Discount, l.LineSubtotal)).ToList();

            await _emailServices.QueueAndSaveAsync(new EmailMessage(
                client.ClientEmail, client.ClientName, $"Proforma {number} - SAM DESIGN",
                EmailTemplates.Proforma(client.ClientName, number, issued, Proforma.ValidDays, lines, order.Subtotal, discount, order.IvaTotal,
                    order.OrderTotalAmount, order.ApplyIva, order.Notes),
                EmailKinds.Proforma, "Order", order.OrderID.ToString()), user);

            await _eventlogServices.LogAsync(typeEvent.Edit, ModuleName, "Order", order.OrderID.ToString(),
                $"Proforma {number} enviada por correo a {client.ClientEmail}.");
            return OperationResult.Ok(order.OrderID);
        }
    }
}
