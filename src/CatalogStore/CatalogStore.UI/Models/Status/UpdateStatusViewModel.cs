using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.Status
{
    public class UpdateStatusViewModel
    {
        public int StatusID { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? UpdatedBy { get; set; }
    }
}
