using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.Status
{
    public class AddStatusViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }
}
