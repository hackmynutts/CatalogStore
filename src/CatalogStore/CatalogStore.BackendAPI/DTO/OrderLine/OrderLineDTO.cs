using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.OrderLine
{
    public class OrderLineDTO
    {
        [Display(Name = "#")]
        public int OrderLineID { get; set; }
        [Display(Name = "Orden")]
        public int OrderID { get; set; }
        [Display(Name = "Producto")]
        public int ProductID { get; set; }
        // Copia guardada al agregar la línea: no cambia aunque el proveedor renombre el producto.
        [Display(Name = "Código del producto")]
        public string? ProductCode { get; set; }
        [Display(Name = "Nombre del producto")]
        public string ProductName { get; set; } = string.Empty;
        [Display(Name = "Cantidad")]
        public int Quantity { get; set; }
        [Display(Name = "Precio unitario")]
        public decimal UnitPrice { get; set; }
        [Display(Name = "Precio unitario con IVA")]
        public decimal UnitPriceIVA { get; set; }
        [Display(Name = "Descuento")]
        public decimal Discount { get; set; } = 0.0m;
        // Sin IVA y con el descuento aplicado. Se calcula al mapear, no se guarda.
        [Display(Name = "Subtotal de la línea")]
        public decimal LineSubtotal { get; set; }
        // LineTotalPrice - LineSubtotal. Se calcula al mapear, no se guarda.
        [Display(Name = "IVA de la línea")]
        public decimal LineIVA { get; set; }
        [Display(Name = "Total de la línea")]
        public decimal LineTotalPrice { get; set; }
        [Display(Name = "Creado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name = "Modificado por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
