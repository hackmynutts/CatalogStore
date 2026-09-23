using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.InventoryLine
{
    [Table("InventoryLine_TB")]
    public class InventoryLine
    {
        public int InventoryLineID { get; set; }
        public int InventoryID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public int QuantityAvailable { get; set; }
        public int QuantityReorder { get; set; }
        public int QuantityOnHold { get; set; }
        public DateOnly LastRestock { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
        public Models.Status.Status Status { get; set; } = null!;
        public Models.Inventory.Inventory Inventory { get; set; } = null!;
        public Models.Product.Product Product { get; set; } = null!;
    }
}
