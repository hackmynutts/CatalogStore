namespace CatalogStore.BackendAPI.Models.Inventory
{
    public class Inventory
    {
        public int InventoryID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public Models.Status.Status Status { get; set; } = null!;
    }
}
