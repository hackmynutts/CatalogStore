using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CatalogStore.BackendAPI.Models.Client
{
    public enum credit
    {
        [Display(Name ="Contado")]
        D0 = 0,
        [Display(Name ="1 semana")]
        D7 = 1,
        [Display(Name ="2 semanas")]
        D14 = 2,
        [Display(Name ="3 semanas")]
        D21 = 3,
        [Display(Name ="1 mes")]
        M1 = 4,
        [Display(Name ="1 mes y medio")]
        M15 = 5
    }
    // Tipo de identificación según el catálogo de Hacienda: el número del enum ES el código (01, 02...),
    // así la factura electrónica lo envía con ((int)tipo).ToString("D2") sin tabla de traducción.
    public enum IdentificationType
    {
        [Display(Name = "Cédula física")]
        Fisica = 1,
        [Display(Name = "Cédula jurídica")]
        Juridica = 2,
        [Display(Name = "DIMEX")]
        Dimex = 3,
        [Display(Name = "NITE")]
        Nite = 4,
        [Display(Name = "Extranjero")]
        Extranjero = 5
    }
    [Table("Client_TB")]
    public class Client
    {
        public int Id { get; set; }
        public IdentificationType IdentificationType { get; set; } = IdentificationType.Fisica;
        public string Identification { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public credit? Credito { get; set; } = credit.D0;
        public string? Cabys { get; set; }
        public string? CC { get; set; }
        public string? DeliveryPartner { get; set; }
        public int StatusID { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; } 
        public DateTime? ModifiedOn { get; set; }
        public Models.Status.Status Status { get; set; } = null!;
    }
}
