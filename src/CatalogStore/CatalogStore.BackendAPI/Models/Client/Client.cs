using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Client
{
    [Table("Client_TB")]
    public class Client
    {
        public int Id { get; set; }
        public string Identification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; } 
        public DateTime? ModifiedOn { get; set; }
        public Models.Status.Status Status { get; set; } = null!;
    }
}
