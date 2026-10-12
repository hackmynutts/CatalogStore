using CatalogStore.BackendAPI.DTO.Common;
using CatalogStore.BackendAPI.DTO.OrderLine;
using CatalogStore.BackendAPI.Repository.InventoryLines;
using CatalogStore.BackendAPI.Repository.Orders;
using CatalogStore.BackendAPI.Repository.Product;
using CatalogStore.BackendAPI.Services.Client;
using CatalogStore.BackendAPI.Services.EventLogs;
using CatalogStore.BackendAPI.Services.InventoryTransactions;
using Microsoft.Extensions.Options;

namespace CatalogStore.BackendAPI.Services.Orders
{
    public class OrderServices : IOrderServices
    {
        private const string ModuleName = "Ventas/Órdenes";
        private const string OrdersTable = "Order";
        private readonly IOrderRepository _orderRepository;
        private readonly IInventoryLineRepository _inventoryLineRepository;
        private readonly IInventoryTransactionServices _inventoryTransactionServices;
        private readonly IProductRepository _productRepository;
        private readonly IClientServices _clientServices;
        private readonly IEventlogServices _eventlogServices;
        private IOptions<PricingOptions> _pricing;
        private IOptions<OrdersOptions> _ordersOptions;
        private TimeProvider _time;


        public OrderServices(IOrderRepository orderRepository, IInventoryLineRepository inventoryLineRepository, IInventoryTransactionServices inventoryTransactionServices, IProductRepository productRepository, IClientServices clientServices, IEventlogServices eventlogServices, IOptions<PricingOptions> pricingOptions, IOptions<OrdersOptions> ordersOptions, TimeProvider time)
        {
            _orderRepository = orderRepository;
            _inventoryLineRepository = inventoryLineRepository;
            _inventoryTransactionServices = inventoryTransactionServices;
            _productRepository = productRepository;
            _clientServices = clientServices;
            _eventlogServices = eventlogServices;
            _pricing = pricingOptions;
            _ordersOptions = ordersOptions;
            _time = time;
        }
        public async Task<OperationResult> AddItemAsync(int clientId, AddOrderLineDTO dto, string user, bool isAdmin)
        {
            var client = await _clientServices.GetClientAsync(clientId);
            if (client == null || client.StatusID != 1)
                return OperationResult.Fail("El cliente no existe o no está activo.");

            if (!isAdmin && dto.Discount > _ordersOptions.Value.MaxSellerDiscountPercent)
                return OperationResult.Fail($"El descuento máximo permitido para vendedores es {_ordersOptions.Value.MaxSellerDiscountPercent}%.");

            var product = await _productRepository.GetByIdsAsync(new[] { dto.ProductID });

        }
    }
}