namespace CatalogStore.UI.Models.InventoryLine
{
    public class InventoryLineViewModel
    {
        public int InventoryLineID { get; set; }
        public int InventoryID { get; set; }
        public int ProductID { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int QuantityOnHold { get; set; }
        public int QuantityAvailable { get; set; }
        public int QuantityRestock { get; set; }
        public bool NeedsRestock { get; set; }
        public DateOnly? LastRestock { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
