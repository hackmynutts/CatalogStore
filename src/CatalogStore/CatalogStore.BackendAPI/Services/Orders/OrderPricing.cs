namespace CatalogStore.BackendAPI.Services.Orders
{
    // Cálculo de precios de una orden. Es una clase pura: no usa la base ni la configuración, todo llega por parámetro.
    // El IVA de una línea se calcula sobre su subtotal (como en una factura), así que el total de la línea es
    // subtotal + IVA y no cantidad × precio con IVA.
    public static class OrderPricing
    {
        // Precio de venta sin IVA = costo del producto × factor de ganancia (1.07 = 7 %).
        // El servicio rechaza los productos sin precio antes de llamar aquí: un 0 silencioso vendería gratis.
        public static decimal UnitPrice(decimal price, decimal profitFactor) =>
            Round2(price * profitFactor);

        // Precio unitario para mostrar (con IVA). No entra en el total de la línea.
        public static decimal PriceWithIva(decimal unitPrice, decimal ivaRate) =>
            Round2(unitPrice * ivaRate);

        // Sin IVA y con el descuento aplicado. El descuento es un porcentaje (10 = 10 %).
        public static decimal LineSubtotal(decimal unitPrice, int quantity, decimal discountPercent) =>
            Round2(unitPrice * quantity * (1 - discountPercent / 100m));

        // ivaRate es el multiplicador (1.13), así que el impuesto es ivaRate - 1 (0.13).
        public static decimal LineIva(decimal lineSubtotal, decimal ivaRate) =>
            Round2(lineSubtotal * (ivaRate - 1));

        // Lo que se guarda en OrderLine.LineTotalPrice. Ambos sumandos ya están redondeados a 2 decimales,
        // por eso la suma es exacta y no se vuelve a redondear.
        public static decimal LineTotalPrice(decimal lineSubtotal, decimal lineIva) =>
            lineSubtotal + lineIva;

        // Redondeo comercial (2.345 → 2.35), el mismo criterio de ExternalProductMapper.RoundMoney.
        private static decimal Round2(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
