namespace CatalogStore.UI.Models.Inventory
{
    public class AddInventoryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
