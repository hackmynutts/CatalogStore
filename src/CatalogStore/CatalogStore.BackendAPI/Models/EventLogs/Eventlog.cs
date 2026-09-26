using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.EventLogs
{
    public enum typeEvent
    {
        None = 0,
        Add = 1,
        AddFail = 2,
        Edit = 3,
        EditFail = 4,
        Delete = 5,
        DeleteFail = 6,
        LogIn = 7,
        LogOut = 8,
        LogInFail = 9,
        LogOutFail = 10,
        Register = 11,
        RegisterFail = 12,
        Inactivate = 13,
        InactivateFail = 14,
        Import = 15,
        ImportFail= 16,
        Error = 17
    }

    [Table("Eventlog_TB")]
    public class Eventlog
    {
        public int EventlogId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string RecordID { get; set; } = string.Empty;
        public typeEvent TypeLog  { get; set; }
        public string EventDesc { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string PreData { get; set; } = string.Empty;
        public string PostData { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; } 
    }
}
