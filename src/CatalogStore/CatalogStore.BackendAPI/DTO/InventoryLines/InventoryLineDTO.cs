using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.InventoryLines
{
    public class InventoryLineDTO
    {
        [Display(Name = "#")]
        public int InventoryLineID { get; set; }
        [Display(Name = "Inventario")]
        public int InventoryID { get; set; }
        [Display(Name = "Producto")]
        public int ProductID { get; set; }
        [Display(Name = "Código del Producto")]
        public string ProductCode { get; set; } = string.Empty;
        [Display(Name = "Nombre del Producto")]
        public string ProductName { get; set; } = string.Empty;
        [Display(Name = "Cantidad")]
        public int Quantity { get; set; }
        [Display(Name = "Cantidad Disponible")]
        public int QuantityAvailable { get; set; }
        [Display(Name = "Cantidad limite")]
        public int QuantityRestock { get; set; }
        [Display(Name = "Cantidad en Espera")]
        public int QuantityOnHold { get; set; }
        [Display(Name = "Último Reabastecimiento")]
        public DateOnly? LastRestock { get; set; }
        [Display(Name = "Necesita Reabastecer")]
        public bool NeedsRestock { get; set; }
        [Display(Name = "Estado")]
        public int StatusID { get; set; }
        [Display(Name = "Creado Por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Creado el")]
        public DateTime CreatedOn { get; set; }
        [Display(Name = "Modificado Por")]
        public string? ModifiedBy { get; set; }
        [Display(Name = "Modificado el")]
        public DateTime? ModifiedOn { get; set; }
    }
}
