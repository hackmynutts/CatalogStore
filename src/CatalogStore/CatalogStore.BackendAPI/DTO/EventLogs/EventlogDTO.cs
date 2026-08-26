using CatalogStore.BackendAPI.Models.EventLogs;
using System.ComponentModel.DataAnnotations;

namespace CatalogStore.BackendAPI.DTO.EventLogs
{
    public class EventlogDTO
    {
        [Display(Name = "#")]
        public int EventlogId { get; set; }
        [Display(Name ="Modulo")]
        [Required]
        public string ModuleName { get; set; } = string.Empty;
        [Display(Name = "Tabla")]
        [Required]
        public string TableName { get; set; } = string.Empty;
        [Display(Name = "Record")]
        [Required]
        public string RecordID { get; set; } = string.Empty;
        [Display(Name = "Accion")]
        [Required]
        public typeEvent TypeLog { get; set; }
        [Display(Name = "Descripcion")]
        [Required]
        public string EventDesc { get; set; } = string.Empty;
        [Display(Name = "StackTrace")]
        public string StackTrace { get; set; } = string.Empty;
        [Display(Name = "Antes")]
        public string PreData { get; set; } = string.Empty;
        [Display(Name = "Despues")]
        public string PostData { get; set; } = string.Empty;
        [Display(Name = "Ejecutado por")]
        public string CreatedBy { get; set; } = string.Empty;
        [Display(Name = "Ejecutado el")]
        public DateTime CreatedOn { get; set; }
    }
}
