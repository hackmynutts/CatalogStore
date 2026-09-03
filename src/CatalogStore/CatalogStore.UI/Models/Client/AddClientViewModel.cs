namespace CatalogStore.UI.Models.Client
{
    public class AddClientViewModel
    {
        public string Identification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string? Cabys { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
