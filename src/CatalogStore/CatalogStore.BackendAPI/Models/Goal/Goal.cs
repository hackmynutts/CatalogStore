using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Goal
{
    [Table("Goal_TB")]
    public class Goal
    {
        public int GoalID { get; set; }
        public string Seller { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal SalesAmountTarget { get; set; }
        public decimal CollectedAmountTarget { get; set; }
        public int SalesCountTarget { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}
