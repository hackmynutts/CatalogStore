namespace CatalogStore.UI.Models.Inventory
{
    public class UpdateInventoryViewModel
    {
        public int InventoryID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int StatusID { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
