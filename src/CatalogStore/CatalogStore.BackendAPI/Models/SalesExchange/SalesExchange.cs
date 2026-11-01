using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.SalesExchange
{
    // Cambio por producto defectuoso dentro de una venta: el cliente devuelve unidades malas de una línea y recibe las mismas
    // unidades del mismo producto. No mueve dinero ni cambia los totales de la venta; solo deja este registro y el movimiento
    // de inventario Merma. Nunca se edita ni se borra: es historial.
    [Table("SalesExchange_TB")]
    public class SalesExchange
    {
        public int SalesExchangeID { get; set; }
        public int SalesHeaderID { get; set; }
        public int SalesLineID { get; set; }
        public int ProductID { get; set; }
        // Copia del producto al momento del cambio: el historial no cambia si luego se edita el producto.
        public string? ProductCode { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public Models.SalesHeader.SalesHeader SalesHeader { get; set; } = null!;
    }
}
