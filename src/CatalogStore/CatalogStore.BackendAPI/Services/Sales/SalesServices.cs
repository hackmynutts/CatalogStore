using CatalogStore.BackendAPI.Models.Client;
using CatalogStore.BackendAPI.Models.Order;
using CatalogStore.BackendAPI.Models.OrderLine;
using CatalogStore.BackendAPI.Models.SalesHeader;
using CatalogStore.BackendAPI.Models.SalesLine;
using CatalogStore.BackendAPI.Repository.Sales;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.Orders;

namespace CatalogStore.BackendAPI.Services.Sales
{
    public class SalesServices : ISalesServices
    {
        private readonly ISalesRepository _salesRepository;
        private readonly IClientServices _clientServices;
        private readonly TimeProvider _time;
        public SalesServices( ISalesRepository salesRepository, IClientServices clientServices, TimeProvider time)
        {
            _salesRepository = salesRepository;
            _clientServices= clientServices;
            _time = time;
        }
        public async Task<SalesHeader> CreateFromOrderAsync(Order order, string createdBy, DateTime now)
        {
            Models.Client.Client cliente = await _clientServices.GetClientAsync(order.ClientID);
            if (cliente is null)
                throw new InvalidOperationException($"El cliente debe de existir para generarle una venta a la orden {order.OrderNumber}");

            var lineasVTA = new List<SalesLine>();
            decimal discountTotal = 0m;
            foreach (var line in order.OrderLines.OrderBy(l => l.OrderLineID))
            {
                var subtotalLine = OrderPricing.LineSubtotal(line.UnitPrice, line.Quantity, line.Discount);
                lineasVTA.Add(new SalesLine
                {
                    ProductID = line.ProductID,
                    ProductCode = line.ProductCode,
                    ProductName = line.ProductName,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitPriceIVA = line.UnitPriceIVA,
                    Discount = line.Discount,
                    LineSubtotal = subtotalLine,
                    LineTotalPrice = line.LineTotalPrice,
                    LineIva = (line.LineTotalPrice- subtotalLine),
                    CreatedBy = createdBy,
                    CreatedOn = now
                });
                discountTotal += ((line.UnitPrice*line.Quantity)-subtotalLine);
            }
            var subtotal = lineasVTA.Sum(l => l.LineSubtotal);
            var ivaTotal = lineasVTA.Sum(l => l.LineIva);
            var plazo = cliente.Credito ?? credit.D0;
            var dias = PaymentTerms.Days(plazo);

            int next = await _salesRepository.NextSalesNumberAsync();
            
            var sale = new SalesHeader
            {
                OrderID = order.OrderID,
                SaleNumber = $"VTA{next:D7}",
                ClientID = order.ClientID,
                ClientIdentificationType = cliente.IdentificationType,
                ClientIdentification = cliente.Identification,
                ClientName = cliente.ClientName,
                ClientEmail = cliente.ClientEmail,
                ClientPhone = cliente.ClientPhone,
                ClientAddress = cliente.ClientAddress,
                ClientCC = cliente.CC,
                SaleDate = now,
                Seller = order.CreatedBy,
                ApplyIva = order.ApplyIva,
                Notes = order.Notes,
                PaymentTerm = plazo,
                DueDate = DateOnly.FromDateTime(_time.GetLocalNow().DateTime.AddDays(dias)),
                CreatedBy = createdBy,
                CreatedOn = now,
                TotalAmount = order.OrderTotalAmount,
                Subtotal = subtotal,
                IvaTotal = ivaTotal,
                DiscountTotal = discountTotal,
                Lines = lineasVTA
            };

            if ((sale.Subtotal + sale.IvaTotal) != sale.TotalAmount)
                throw new InvalidOperationException($"Los montos no coinciden entre el subtotal de la venta {sale.Subtotal} junto con el iva total {sale.IvaTotal} " +
                                                    $"es diferente al monto de la venta {sale.TotalAmount}, con una diferencia de {sale.TotalAmount- (sale.Subtotal + sale.IvaTotal)}");

            _salesRepository.AddSale(sale);
            return sale;
        }
    }
}
