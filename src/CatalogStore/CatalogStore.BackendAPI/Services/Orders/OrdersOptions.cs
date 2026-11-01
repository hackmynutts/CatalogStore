namespace CatalogStore.BackendAPI.Services.Orders
{
    public class OrdersOptions
    {
        public decimal MaxSellerDiscountPercent { get; set; } = 5m;
        public int PendingExpirationDays { get; set; } = 8;
        public int PendingWarningDays { get; set; } = 6;
        // Un cliente con esta cantidad (o más) de ventas vencidas no puede iniciar ni completar órdenes. 0 o menos = función apagada.
        public int BlockOverdueSalesFrom { get; set; } = 2;
    }
}
