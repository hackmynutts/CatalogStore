using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CatalogStore.BackendAPI.Models.ProductImage
{
    [Table("ProductImages_TB")]
    public class ProductImage
    {
        public int ProductImageID{ get; set; }
        public int ProductID{ get; set; }
        public string Url{ get; set; } = string.Empty;
        public string? ContentType{ get; set; } // image/jpeg, image/png
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [JsonIgnore]
        public Models.Product.Product Product{ get; set; } = null!;
    }
}
