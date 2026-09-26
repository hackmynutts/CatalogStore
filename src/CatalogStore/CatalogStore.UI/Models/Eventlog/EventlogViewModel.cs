using System.ComponentModel.DataAnnotations;

namespace CatalogStore.UI.Models.Eventlog
{
    public enum typeEvent
    {
        None = 0,
        [Display(Name = "Creación")]
        Add = 1,
        [Display(Name = "Creación fallida")]
        AddFail = 2,
        [Display(Name = "Edición")]
        Edit = 3,
        [Display(Name = "Edición fallida")]
        EditFail = 4,
        [Display(Name = "Eliminación")]
        Delete = 5,
        [Display(Name = "Eliminacion fallida")]
        DeleteFail = 6,
        [Display(Name = "Inicio de sesión")]
        LogIn = 7,
        [Display(Name = "Cierre de sesión")]
        LogOut = 8,
        [Display(Name = "Inicio de sesión fallido")]
        LogInFail = 9,
        [Display(Name = "Cierre de sesión fallido")]
        LogOutFail = 10,
        [Display(Name = "Registro")]
        Register = 11,
        [Display(Name = "Registro fallido")]
        RegisterFail = 12,
        [Display(Name = "Inactivación")]
        Inactivate = 13,
        [Display(Name = "Inactivación fallida")]
        InactivateFail = 14,
        [Display(Name = "Importación")]
        Import = 15,
        [Display(Name = "Importación fallida")]
        ImportFail = 16,
        [Display(Name = "Error")]
        Error = 17
    }
    public class EventlogViewModel
    {
        public int EventlogId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string RecordID { get; set; } = string.Empty;
        public typeEvent TypeLog { get; set; }
        public string EventDesc { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public string PreData { get; set; } = string.Empty;
        public string PostData { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
    }
}
