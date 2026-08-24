namespace CatalogStore.UI.Models.Status
{
    public class StatusViewModel
    {
        public int StatusID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
