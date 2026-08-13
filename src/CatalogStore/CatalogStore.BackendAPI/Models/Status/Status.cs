using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Status
{
    [Table("Status_TB")]
    public class Status
    {
        public int StatusID{ get; set; }
        public string name{ get; set; } = string.Empty; 
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get;set; }
    }
}
