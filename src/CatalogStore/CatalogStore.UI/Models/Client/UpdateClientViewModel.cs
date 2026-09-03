namespace CatalogStore.UI.Models.Client
{
    public class UpdateClientViewModel
    {
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string? Cabys { get; set; }
        public int StatusID { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
